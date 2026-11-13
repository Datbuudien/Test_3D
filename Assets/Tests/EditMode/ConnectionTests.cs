using System.Text;
using NUnit.Framework;

public class ConnectionTests
{
    private const string CHARACTER_ID = "polish_soldier";
    private const string WEAPON_ID = "grenade";
    private const string SCENARIO_NAME = "Demo 1";

    [Test]
    public void FromBytes_RoundTrip_KeepsAllFields()
    {
        byte[] bytes = ConnectionPayload.Create(CHARACTER_ID, WEAPON_ID, SCENARIO_NAME).ToBytes();

        ConnectionPayload payload = ConnectionPayload.FromBytes(bytes);

        Assert.AreEqual(CHARACTER_ID, payload.characterId);
        Assert.AreEqual(WEAPON_ID, payload.weaponId);
        Assert.AreEqual(SCENARIO_NAME, payload.scenarioName);
    }

    [Test]
    public void FromBytes_NullOrEmpty_ReturnsEmptyPayload()
    {
        AssertEmpty(ConnectionPayload.FromBytes(null));
        AssertEmpty(ConnectionPayload.FromBytes(new byte[0]));
    }

    [Test]
    public void FromBytes_CorruptJson_ReturnsEmptyPayloadWithoutThrowing()
    {
        ConnectionPayload payload = null;

        Assert.DoesNotThrow(() => payload = ConnectionPayload.FromBytes(Encoding.UTF8.GetBytes("{ not json")));
        AssertEmpty(payload);
    }

    [Test]
    public void FromBytes_OversizedPayload_ReturnsEmptyPayload()
    {
        byte[] bytes = new byte[ConnectionPayload.MAX_PAYLOAD_BYTES + 1];

        AssertEmpty(ConnectionPayload.FromBytes(bytes));
    }

    [Test]
    public void FromBytes_LongText_ClampedToMaxLength()
    {
        string longName = new string('a', ConnectionPayload.MAX_TEXT_LENGTH * 2);
        byte[] bytes = Encoding.UTF8.GetBytes("{\"scenarioName\":\"" + longName + "\"}");

        ConnectionPayload payload = ConnectionPayload.FromBytes(bytes);

        Assert.AreEqual(ConnectionPayload.MAX_TEXT_LENGTH, payload.scenarioName.Length);
        Assert.AreEqual(string.Empty, payload.characterId);
    }

    [TestCase("127.0.0.1", "127.0.0.1")]
    [TestCase(" 192.168.1.20 ", "192.168.1.20")]
    public void TryParseAddress_ValidIpv4_ReturnsTrimmedAddress(string raw, string expected)
    {
        Assert.IsTrue(ConnectionEndpoint.TryParseAddress(raw, out string address));
        Assert.AreEqual(expected, address);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    [TestCase("1")]
    [TestCase("192.168.1")]
    [TestCase("256.1.1.1")]
    [TestCase("localhost")]
    [TestCase("::1")]
    public void TryParseAddress_Invalid_ReturnsFalse(string raw)
    {
        Assert.IsFalse(ConnectionEndpoint.TryParseAddress(raw, out _));
    }

    [TestCase("7777", (ushort)7777)]
    [TestCase(" 65535 ", (ushort)65535)]
    public void TryParsePort_Valid_ReturnsPort(string raw, ushort expected)
    {
        Assert.IsTrue(ConnectionEndpoint.TryParsePort(raw, out ushort port));
        Assert.AreEqual(expected, port);
    }

    [TestCase("")]
    [TestCase(null)]
    [TestCase("0")]
    [TestCase("65536")]
    [TestCase("-1")]
    [TestCase("abc")]
    public void TryParsePort_Invalid_ReturnsFalse(string raw)
    {
        Assert.IsFalse(ConnectionEndpoint.TryParsePort(raw, out _));
    }

    private static void AssertEmpty(ConnectionPayload payload)
    {
        Assert.IsNotNull(payload);
        Assert.AreEqual(string.Empty, payload.characterId);
        Assert.AreEqual(string.Empty, payload.weaponId);
        Assert.AreEqual(string.Empty, payload.scenarioName);
    }
}
