using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class ScenarioRepository
{
    public const string FOLDER_NAME = "Scenarios";
    public const string FILE_EXTENSION = ".json";
    public const int MAX_FILE_NAME_LENGTH = 64;

    private const string SEARCH_PATTERN = "*" + FILE_EXTENSION;
    private const char REPLACEMENT_CHAR = '_';
    private const string RESERVED_PREFIX = "_";
    // Path.GetInvalidFileNameChars tren macOS/Linux chi co '/' va '\0' -> bo sung bo ky tu cam cua Windows
    private const string WINDOWS_INVALID_CHARS = "<>:\"/\\|?*";
    private static readonly char[] TRAILING_TRIM_CHARS = { '.', ' ' };
    // Encoding.UTF8 ghi BOM o dau file, mot so trinh doc JSON khong chap nhan
    private static readonly Encoding UTF8_NO_BOM = new UTF8Encoding(false);
    private static readonly HashSet<string> WINDOWS_RESERVED_NAMES = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public ScenarioRepository(string rootDirectory)
    {
        RootDirectory = rootDirectory;
    }

    public static string DefaultDirectory => Path.Combine(Application.persistentDataPath, FOLDER_NAME);

    public string RootDirectory { get; }

    public static string SanitizeFileName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(rawName.Trim());
        char[] invalidChars = Path.GetInvalidFileNameChars();
        for (int index = 0; index < builder.Length; index++)
        {
            char current = builder[index];
            if (Array.IndexOf(invalidChars, current) >= 0 || WINDOWS_INVALID_CHARS.IndexOf(current) >= 0)
            {
                builder[index] = REPLACEMENT_CHAR;
            }
        }

        string sanitized = builder.ToString();
        if (sanitized.Length > MAX_FILE_NAME_LENGTH)
        {
            sanitized = sanitized.Substring(0, MAX_FILE_NAME_LENGTH);
        }

        // Windows tu bo dau cham/khoang trang cuoi ten file -> bo truoc de ten luu ra khop ten tra ve
        sanitized = sanitized.TrimEnd(TRAILING_TRIM_CHARS);
        if (sanitized.Length == 0)
        {
            return string.Empty;
        }

        return IsWindowsReservedName(sanitized) ? RESERVED_PREFIX + sanitized : sanitized;
    }

    public string GetFilePath(string scenarioName)
    {
        string fileName = SanitizeFileName(scenarioName);
        return fileName.Length == 0 ? string.Empty : Path.Combine(RootDirectory, fileName + FILE_EXTENSION);
    }

    public bool Exists(string scenarioName)
    {
        string filePath = GetFilePath(scenarioName);
        return filePath.Length > 0 && File.Exists(filePath);
    }

    public bool TryEnsureRootDirectory()
    {
        try
        {
            Directory.CreateDirectory(RootDirectory);
            return true;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            return false;
        }
    }

    public SaveResult Save(ScenarioData data, bool overwrite)
    {
        string filePath = data != null ? GetFilePath(data.scenarioName) : string.Empty;
        if (filePath.Length == 0)
        {
            return new SaveResult(SaveStatus.EmptyName, string.Empty, string.Empty);
        }

        if (!overwrite && File.Exists(filePath))
        {
            return new SaveResult(SaveStatus.AlreadyExists, filePath, string.Empty);
        }

        try
        {
            Directory.CreateDirectory(RootDirectory);
            File.WriteAllText(filePath, JsonUtility.ToJson(data, true), UTF8_NO_BOM);
            return new SaveResult(SaveStatus.Success, filePath, string.Empty);
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            return new SaveResult(SaveStatus.IoError, filePath, exception.Message);
        }
    }

    public bool TryLoad(string filePath, out ScenarioData data)
    {
        data = null;
        try
        {
            ScenarioData loaded = JsonUtility.FromJson<ScenarioData>(File.ReadAllText(filePath, Encoding.UTF8));
            if (loaded == null || !loaded.HasRequiredFields())
            {
                return false;
            }

            data = loaded;
            return true;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException
            || exception is ArgumentException)
        {
            return false;
        }
    }

    public List<ScenarioData> LoadAll()
    {
        List<ScenarioData> scenarios = new List<ScenarioData>();
        if (!Directory.Exists(RootDirectory))
        {
            return scenarios;
        }

        string[] filePaths;
        try
        {
            filePaths = Directory.GetFiles(RootDirectory, SEARCH_PATTERN);
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            return scenarios;
        }

        foreach (string filePath in filePaths)
        {
            if (TryLoad(filePath, out ScenarioData data))
            {
                scenarios.Add(data);
            }
        }

        // createdAt dang ISO "yyyy-MM-ddTHH:mm:ss" -> so chuoi ordinal dung thu tu thoi gian
        scenarios.Sort((left, right) => string.CompareOrdinal(right.createdAt, left.createdAt));
        return scenarios;
    }

    private static bool IsWindowsReservedName(string fileName)
    {
        // Windows chan ca "CON.txt" -> chi xet phan truoc dau cham dau tien
        int dotIndex = fileName.IndexOf('.');
        string baseName = dotIndex >= 0 ? fileName.Substring(0, dotIndex) : fileName;
        return WINDOWS_RESERVED_NAMES.Contains(baseName.TrimEnd());
    }
}
