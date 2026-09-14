using System.Buffers.Binary;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;

namespace RfidVehicleAccess.Services;

public sealed class MqttPublishService(AppOptions options)
{
    private static int _packetIdentifier;
    private readonly SemaphoreSlim _publishMutex = new(1, 1);

    public async Task PublishAsync(
        string topic,
        string payload,
        CancellationToken cancellationToken = default)
    {
        await _publishMutex.WaitAsync(cancellationToken);
        try
        {
            var mqtt = options.Server.Mqtt;
            ValidateConfiguration(mqtt, topic);

            using var timeoutSource =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(Math.Max(
                5,
                mqtt.ConnectTimeoutSeconds + mqtt.PublishTimeoutSeconds)));
            var token = timeoutSource.Token;

            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(mqtt.BrokerHost, mqtt.Port, token);

            Stream stream = tcpClient.GetStream();
            if (mqtt.UseTls)
            {
                var sslStream = new SslStream(stream, leaveInnerStreamOpen: false);
                await sslStream.AuthenticateAsClientAsync(
                    new SslClientAuthenticationOptions
                    {
                        TargetHost = mqtt.BrokerHost,
                        EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                    },
                    token);
                stream = sslStream;
            }

            await using (stream)
            {
                await SendConnectAsync(stream, mqtt, token);
                await ReadConnAckAsync(stream, token);

                var qos = Math.Clamp(mqtt.QualityOfService, 0, 1);
                var packetId = qos == 1 ? NextPacketIdentifier() : (ushort)0;
                await SendPublishAsync(
                    stream,
                    topic,
                    payload,
                    qos,
                    mqtt.Retain,
                    packetId,
                    token);

                if (qos == 1)
                {
                    await ReadPubAckAsync(stream, packetId, token);
                }

                await stream.WriteAsync(new byte[] { 0xE0, 0x00 }, token);
                await stream.FlushAsync(token);
            }
        }
        finally
        {
            _publishMutex.Release();
        }
    }

    public async Task<string> PublishAndWaitForResponseAsync(
        string publishTopic,
        string subscribeTopic,
        string payload,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        await _publishMutex.WaitAsync(cancellationToken);
        try
        {
            var mqtt = options.Server.Mqtt;
            ValidateConfiguration(mqtt, publishTopic);
            if (string.IsNullOrWhiteSpace(subscribeTopic))
            {
                throw new InvalidOperationException("MQTT trip authorization response topic is empty.");
            }

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);
            var token = timeoutSource.Token;
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(mqtt.BrokerHost, mqtt.Port, token);
            Stream stream = tcpClient.GetStream();
            if (mqtt.UseTls)
            {
                var sslStream = new SslStream(stream, leaveInnerStreamOpen: false);
                await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                {
                    TargetHost = mqtt.BrokerHost,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                }, token);
                stream = sslStream;
            }

            await using (stream)
            {
                await SendConnectAsync(stream, mqtt, token);
                await ReadConnAckAsync(stream, token);
                var subscribePacketId = NextPacketIdentifier();
                await SendSubscribeAsync(stream, subscribeTopic, subscribePacketId, token);
                await ReadSubAckAsync(stream, subscribePacketId, token);

                var qos = Math.Clamp(mqtt.QualityOfService, 0, 1);
                var publishPacketId = qos == 1 ? NextPacketIdentifier() : (ushort)0;
                await SendPublishAsync(stream, publishTopic, payload, qos, false, publishPacketId, token);
                if (qos == 1) await ReadPubAckAsync(stream, publishPacketId, token);

                while (true)
                {
                    var (packetType, packetPayload) = await ReadPacketAsync(stream, token);
                    if (packetType != 3) continue;
                    var response = ParsePublishPayload(packetPayload);
                    if (!string.Equals(response.Topic, subscribeTopic, StringComparison.Ordinal)) continue;
                    return response.Payload;
                }
            }
        }
        finally
        {
            _publishMutex.Release();
        }
    }

    private static async Task SendSubscribeAsync(Stream stream, string topic, ushort packetIdentifier, CancellationToken cancellationToken)
    {
        using var body = new MemoryStream();
        WriteUInt16(body, packetIdentifier);
        WriteMqttString(body, topic);
        body.WriteByte(0x00);
        await WritePacketAsync(stream, 0x82, body.ToArray(), cancellationToken);
    }

    private static async Task ReadSubAckAsync(Stream stream, ushort expectedPacketIdentifier, CancellationToken cancellationToken)
    {
        while (true)
        {
            var (packetType, payload) = await ReadPacketAsync(stream, cancellationToken);
            if (packetType != 9) continue;
            if (payload.Length < 3) throw new IOException("The MQTT broker returned an invalid SUBACK packet.");
            var actual = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(0, 2));
            if (actual != expectedPacketIdentifier) continue;
            if (payload[2] == 0x80) throw new IOException("MQTT subscription was rejected by the broker.");
            return;
        }
    }

    private static (string Topic, string Payload) ParsePublishPayload(byte[] payload)
    {
        if (payload.Length < 2) throw new IOException("Invalid MQTT PUBLISH packet.");
        var topicLength = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(0, 2));
        if (payload.Length < 2 + topicLength) throw new IOException("Invalid MQTT PUBLISH topic length.");
        var topic = Encoding.UTF8.GetString(payload, 2, topicLength);
        // Server authorization response should be QoS 0 for simplest interoperability.
        var text = Encoding.UTF8.GetString(payload, 2 + topicLength, payload.Length - 2 - topicLength);
        return (topic, text);
    }

    private static void ValidateConfiguration(MqttOptions mqtt, string topic)
    {
        if (string.IsNullOrWhiteSpace(mqtt.BrokerHost))
        {
            throw new InvalidOperationException("MQTT BrokerHost is empty.");
        }

        if (mqtt.Port is <= 0 or > 65535)
        {
            throw new InvalidOperationException("MQTT Port must be between 1 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(topic))
        {
            throw new InvalidOperationException("MQTT publish topic is empty.");
        }
    }

    private static async Task SendConnectAsync(
        Stream stream,
        MqttOptions mqtt,
        CancellationToken cancellationToken)
    {
        using var variableHeaderAndPayload = new MemoryStream();
        WriteMqttString(variableHeaderAndPayload, "MQTT");
        variableHeaderAndPayload.WriteByte(0x04); // MQTT 3.1.1

        byte connectFlags = 0x02; // Clean session.
        if (!string.IsNullOrEmpty(mqtt.Username))
        {
            connectFlags |= 0x80;
        }

        if (!string.IsNullOrEmpty(mqtt.Password))
        {
            connectFlags |= 0x40;
        }

        variableHeaderAndPayload.WriteByte(connectFlags);

        var keepAlive = (ushort)Math.Clamp(mqtt.KeepAliveSeconds, 0, ushort.MaxValue);
        WriteUInt16(variableHeaderAndPayload, keepAlive);

        var clientId = string.IsNullOrWhiteSpace(mqtt.ClientId)
            ? $"RfidVehicleAccess-{Environment.MachineName}"
            : mqtt.ClientId.Trim();
        WriteMqttString(variableHeaderAndPayload, clientId);

        if (!string.IsNullOrEmpty(mqtt.Username))
        {
            WriteMqttString(variableHeaderAndPayload, mqtt.Username);
        }

        if (!string.IsNullOrEmpty(mqtt.Password))
        {
            WriteMqttString(variableHeaderAndPayload, mqtt.Password);
        }

        await WritePacketAsync(
            stream,
            0x10,
            variableHeaderAndPayload.ToArray(),
            cancellationToken);
    }

    private static async Task ReadConnAckAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var (packetType, payload) = await ReadPacketAsync(stream, cancellationToken);
        if (packetType != 2 || payload.Length != 2)
        {
            throw new IOException("The MQTT broker returned an invalid CONNACK packet.");
        }

        if ((payload[0] & 0xFE) != 0)
        {
            throw new IOException("The MQTT broker returned invalid CONNACK flags.");
        }

        if (payload[1] != 0)
        {
            throw new IOException($"MQTT connection was rejected. Return code: {payload[1]}.");
        }
    }

    private static async Task SendPublishAsync(
        Stream stream,
        string topic,
        string payload,
        int qualityOfService,
        bool retain,
        ushort packetIdentifier,
        CancellationToken cancellationToken)
    {
        using var packetPayload = new MemoryStream();
        WriteMqttString(packetPayload, topic);

        if (qualityOfService == 1)
        {
            WriteUInt16(packetPayload, packetIdentifier);
        }

        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        packetPayload.Write(payloadBytes, 0, payloadBytes.Length);

        byte fixedHeader = 0x30;
        fixedHeader |= (byte)(qualityOfService << 1);
        if (retain)
        {
            fixedHeader |= 0x01;
        }

        await WritePacketAsync(
            stream,
            fixedHeader,
            packetPayload.ToArray(),
            cancellationToken);
    }

    private static async Task ReadPubAckAsync(
        Stream stream,
        ushort expectedPacketIdentifier,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var (packetType, payload) = await ReadPacketAsync(stream, cancellationToken);
            if (packetType != 4)
            {
                continue;
            }

            if (payload.Length != 2)
            {
                throw new IOException("The MQTT broker returned an invalid PUBACK packet.");
            }

            var actualPacketIdentifier = BinaryPrimitives.ReadUInt16BigEndian(payload);
            if (actualPacketIdentifier != expectedPacketIdentifier)
            {
                continue;
            }

            return;
        }
    }

    private static async Task WritePacketAsync(
        Stream stream,
        byte fixedHeader,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        using var packet = new MemoryStream();
        packet.WriteByte(fixedHeader);
        WriteRemainingLength(packet, payload.Length);
        packet.Write(payload, 0, payload.Length);

        await stream.WriteAsync(packet.ToArray(), cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task<(int PacketType, byte[] Payload)> ReadPacketAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var header = await ReadByteAsync(stream, cancellationToken);
        var remainingLength = await ReadRemainingLengthAsync(stream, cancellationToken);
        var payload = new byte[remainingLength];
        await ReadExactlyAsync(stream, payload, cancellationToken);
        return (header >> 4, payload);
    }

    private static async Task<int> ReadRemainingLengthAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var multiplier = 1;
        var value = 0;

        for (var index = 0; index < 4; index++)
        {
            var encodedByte = await ReadByteAsync(stream, cancellationToken);
            value += (encodedByte & 0x7F) * multiplier;

            if ((encodedByte & 0x80) == 0)
            {
                return value;
            }

            multiplier *= 128;
        }

        throw new IOException("The MQTT remaining-length field is invalid.");
    }

    private static async Task<byte> ReadByteAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[1];
        await ReadExactlyAsync(stream, buffer, cancellationToken);
        return buffer[0];
    }

    private static async Task ReadExactlyAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(
                buffer.AsMemory(offset, buffer.Length - offset),
                cancellationToken);
            if (read == 0)
            {
                throw new EndOfStreamException("The MQTT broker closed the connection unexpectedly.");
            }

            offset += read;
        }
    }

    private static void WriteMqttString(Stream stream, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "MQTT strings cannot exceed 65535 bytes.");
        }

        WriteUInt16(stream, (ushort)bytes.Length);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void WriteUInt16(Stream stream, ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteRemainingLength(Stream stream, int value)
    {
        do
        {
            var encodedByte = value % 128;
            value /= 128;
            if (value > 0)
            {
                encodedByte |= 0x80;
            }

            stream.WriteByte((byte)encodedByte);
        }
        while (value > 0);
    }

    private static ushort NextPacketIdentifier()
    {
        while (true)
        {
            var next = Interlocked.Increment(ref _packetIdentifier);
            var packetIdentifier = (ushort)(next % ushort.MaxValue);
            if (packetIdentifier != 0)
            {
                return packetIdentifier;
            }
        }
    }
}
