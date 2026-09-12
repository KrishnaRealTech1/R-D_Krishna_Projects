using System.Buffers.Binary;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using RfidVehicleAccess.Data;
using RfidVehicleAccess.Models;

namespace RfidVehicleAccess.Services;

public sealed class MqttRechargeSyncWorker(
    AppOptions options,
    RechargeRepository rechargeRepository,
    MqttPublishService mqttPublisher,
    AppLogger logger,
    SystemEventHub eventHub) : BackgroundService
{
    private const int MaximumPacketSize = 1024 * 1024;
    private static int _packetIdentifier;

    private readonly JsonSerializerOptions _readJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly JsonSerializerOptions _writeJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var mqtt = options.Server.Mqtt;
        if (!options.Server.Enabled || !mqtt.RechargeSyncEnabled)
        {
            await logger.ServerAsync(
                "RFID recharge synchronization is disabled.",
                stoppingToken);
            return;
        }

        ValidateConfiguration(mqtt);
        await logger.ServerAsync(
            $"RFID recharge synchronization enabled. Subscribing to {mqtt.RechargeSubscribeTopic}.",
            stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunSubscriptionSessionAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                await logger.ServerAsync(
                    $"RFID recharge MQTT connection error: {GetUsefulErrorMessage(ex)}",
                    stoppingToken);

                await Task.Delay(
                    TimeSpan.FromSeconds(Math.Max(1, mqtt.RechargeReconnectSeconds)),
                    stoppingToken);
            }
        }
    }

    private async Task RunSubscriptionSessionAsync(CancellationToken cancellationToken)
    {
        var mqtt = options.Server.Mqtt;
        using var tcpClient = new TcpClient();
        using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectTimeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, mqtt.ConnectTimeoutSeconds)));

        await tcpClient.ConnectAsync(mqtt.BrokerHost, mqtt.Port, connectTimeout.Token);

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
                connectTimeout.Token);
            stream = sslStream;
        }

        await using (stream)
        {
            await SendConnectAsync(stream, mqtt, cancellationToken);
            await ReadConnAckAsync(stream, cancellationToken);

            var subscribePacketId = NextPacketIdentifier();
            await SendSubscribeAsync(
                stream,
                mqtt.RechargeSubscribeTopic,
                Math.Clamp(mqtt.QualityOfService, 0, 1),
                subscribePacketId,
                cancellationToken);
            await ReadSubAckAsync(stream, subscribePacketId, cancellationToken);

            await logger.ServerAsync(
                $"RFID recharge MQTT subscriber connected to {mqtt.BrokerHost}:{mqtt.Port}.",
                cancellationToken);

            var pingInterval = TimeSpan.FromSeconds(Math.Max(5, mqtt.KeepAliveSeconds / 2));
            var pendingRead = ReadPacketAsync(stream, cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                var completed = await Task.WhenAny(
                    pendingRead,
                    Task.Delay(pingInterval, cancellationToken));

                if (completed != pendingRead)
                {
                    await WritePacketAsync(stream, 0xC0, [], cancellationToken);
                    continue;
                }

                var packet = await pendingRead;
                pendingRead = ReadPacketAsync(stream, cancellationToken);

                switch (packet.PacketType)
                {
                    case 3:
                        await ProcessPublishPacketAsync(stream, packet, cancellationToken);
                        break;
                    case 13:
                        break; // PINGRESP
                    case 14:
                        throw new IOException("The MQTT broker closed the subscriber session.");
                }
            }
        }
    }

    private async Task ProcessPublishPacketAsync(
        Stream stream,
        MqttPacket packet,
        CancellationToken cancellationToken)
    {
        var publish = ParsePublish(packet);
        if (!string.Equals(
                publish.Topic,
                options.Server.Mqtt.RechargeSubscribeTopic,
                StringComparison.Ordinal))
        {
            await AcknowledgeBrokerPublishAsync(stream, publish, cancellationToken);
            return;
        }

        var payload = Encoding.UTF8.GetString(publish.Payload);
        var applicationProcessed = await ProcessRechargePayloadAsync(payload, cancellationToken);
        if (!applicationProcessed)
        {
            await logger.ServerAsync(
                "RFID recharge MQTT message was discarded because it was not valid JSON or had no recharge ID.",
                cancellationToken);
        }

        await AcknowledgeBrokerPublishAsync(stream, publish, cancellationToken);
    }

    private async Task<bool> ProcessRechargePayloadAsync(
        string payload,
        CancellationToken cancellationToken)
    {
        RfidRechargeCommand? command;
        try
        {
            command = JsonSerializer.Deserialize<RfidRechargeCommand>(payload, _readJsonOptions);
        }
        catch (JsonException ex)
        {
            await logger.ServerAsync(
                $"Invalid RFID recharge JSON: {ex.Message}",
                cancellationToken);
            return false;
        }

        if (command is null || string.IsNullOrWhiteSpace(command.RechargeId))
        {
            return false;
        }

        command.RechargeId = command.RechargeId.Trim();
        command.RfidNumber = NormalizeRfid(command.RfidNumber);
        command.VehicleNumber = (command.VehicleNumber ?? string.Empty).Trim();
        command.MessageType = (command.MessageType ?? string.Empty).Trim();
        command.SiteId = (command.SiteId ?? string.Empty).Trim();
        command.DeviceId = (command.DeviceId ?? string.Empty).Trim();
        command.SchemaVersion = (command.SchemaVersion ?? string.Empty).Trim();
        command.PaymentReference = (command.PaymentReference ?? string.Empty).Trim();
        command.OperatorId = (command.OperatorId ?? string.Empty).Trim();
        command.OperatorName = (command.OperatorName ?? string.Empty).Trim();
        command.Remarks = (command.Remarks ?? string.Empty).Trim();

        var validationError = ValidateCommand(command);
        RechargeApplyResult result;
        if (validationError is not null)
        {
            result = new RechargeApplyResult
            {
                RechargeId = command.RechargeId,
                RfidNumber = command.RfidNumber,
                VehicleNumber = command.VehicleNumber,
                Status = "Rejected",
                Success = false,
                RechargeAmount = command.RechargeAmount,
                Message = validationError,
                ProcessedAt = DateTimeOffset.Now
            };
        }
        else
        {
            result = await rechargeRepository.ApplyAsync(command, payload, cancellationToken);
            eventHub.PublishCountersChanged();
        }

        var acknowledgement = BuildAcknowledgement(command, result);
        var acknowledgementJson = JsonSerializer.Serialize(acknowledgement, _writeJsonOptions);
        await mqttPublisher.PublishAsync(
            options.Server.Mqtt.RechargeAckTopic,
            acknowledgementJson,
            cancellationToken);

        await logger.ServerAsync(
            $"RFID recharge {command.RechargeId} for {command.RfidNumber}: " +
            $"{result.Status}. {result.Message}",
            cancellationToken);
        return true;
    }

    private string? ValidateCommand(RfidRechargeCommand command)
    {
        if (!string.Equals(
                command.MessageType,
                "rfidRecharge",
                StringComparison.OrdinalIgnoreCase))
        {
            return "messageType must be rfidRecharge.";
        }

        if (command.RechargeId.Length > 100)
        {
            return "rechargeId must not exceed 100 characters.";
        }

        if (string.IsNullOrWhiteSpace(command.RfidNumber))
        {
            return "rfidNumber is required.";
        }

        if (command.RechargeAmount <= 0m)
        {
            return "rechargeAmount must be greater than zero.";
        }

        if (!string.IsNullOrWhiteSpace(command.SiteId) &&
            !string.Equals(command.SiteId, options.Device.SiteId, StringComparison.OrdinalIgnoreCase))
        {
            return $"Message is for site {command.SiteId}; this device belongs to {options.Device.SiteId}.";
        }

        if (!string.IsNullOrWhiteSpace(command.DeviceId) &&
            !string.Equals(command.DeviceId, options.Device.DeviceId, StringComparison.OrdinalIgnoreCase))
        {
            return $"Message is for device {command.DeviceId}; this device is {options.Device.DeviceId}.";
        }

        if (command.PreviousBalance is { } previousBalance &&
            command.NewBalance is { } newBalance &&
            Math.Abs(previousBalance + command.RechargeAmount - newBalance) > 0.01m)
        {
            return "previousBalance + rechargeAmount does not equal newBalance.";
        }

        return null;
    }

    private RfidRechargeAcknowledgement BuildAcknowledgement(
        RfidRechargeCommand command,
        RechargeApplyResult result)
    {
        bool? serverBalanceMatched = null;
        if (result.NewBalance is { } localNewBalance && command.NewBalance is { } serverNewBalance)
        {
            serverBalanceMatched = Math.Abs(localNewBalance - serverNewBalance) <= 0.01m;
        }

        return new RfidRechargeAcknowledgement
        {
            RechargeId = command.RechargeId,
            SiteId = options.Device.SiteId,
            DeviceId = options.Device.DeviceId,
            LaneId = options.Device.LaneId,
            DeviceName = options.Device.DeviceName,
            RfidNumber = result.RfidNumber,
            VehicleNumber = result.VehicleNumber,
            Status = result.Status,
            Success = result.Success,
            Duplicate = result.IsDuplicate,
            RechargeAmount = result.RechargeAmount,
            PreviousBalance = result.PreviousBalance,
            NewBalance = result.NewBalance,
            ServerExpectedNewBalance = command.NewBalance,
            ServerBalanceMatched = serverBalanceMatched,
            ProcessedAt = result.ProcessedAt,
            Message = result.Message
        };
    }

    private static MqttPublishPacket ParsePublish(MqttPacket packet)
    {
        var payload = packet.Payload;
        if (payload.Length < 2)
        {
            throw new IOException("MQTT PUBLISH packet does not contain a topic.");
        }

        var topicLength = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(0, 2));
        var offset = 2;
        if (topicLength == 0 || offset + topicLength > payload.Length)
        {
            throw new IOException("MQTT PUBLISH packet contains an invalid topic length.");
        }

        var topic = Encoding.UTF8.GetString(payload, offset, topicLength);
        offset += topicLength;

        var qualityOfService = (packet.Header >> 1) & 0x03;
        ushort? packetIdentifier = null;
        if (qualityOfService > 0)
        {
            if (offset + 2 > payload.Length)
            {
                throw new IOException("MQTT PUBLISH packet does not contain a packet identifier.");
            }

            packetIdentifier = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(offset, 2));
            offset += 2;
        }

        if (qualityOfService > 1)
        {
            throw new IOException("MQTT QoS 2 recharge messages are not supported.");
        }

        return new MqttPublishPacket(
            topic,
            qualityOfService,
            packetIdentifier,
            payload[offset..]);
    }

    private static async Task AcknowledgeBrokerPublishAsync(
        Stream stream,
        MqttPublishPacket publish,
        CancellationToken cancellationToken)
    {
        if (publish.QualityOfService != 1 || publish.PacketIdentifier is null)
        {
            return;
        }

        var packetId = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(packetId, publish.PacketIdentifier.Value);
        await WritePacketAsync(stream, 0x40, packetId, cancellationToken);
    }

    private static void ValidateConfiguration(MqttOptions mqtt)
    {
        if (string.IsNullOrWhiteSpace(mqtt.BrokerHost))
        {
            throw new InvalidOperationException("MQTT BrokerHost is empty.");
        }

        if (mqtt.Port is <= 0 or > 65535)
        {
            throw new InvalidOperationException("MQTT Port must be between 1 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(mqtt.RechargeSubscribeTopic))
        {
            throw new InvalidOperationException("MQTT RechargeSubscribeTopic is empty.");
        }

        if (string.IsNullOrWhiteSpace(mqtt.RechargeAckTopic))
        {
            throw new InvalidOperationException("MQTT RechargeAckTopic is empty.");
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

        byte connectFlags = 0x00; // Persistent session for queued QoS 1 recharge commands.
        if (!string.IsNullOrEmpty(mqtt.Username))
        {
            connectFlags |= 0x80;
        }

        if (!string.IsNullOrEmpty(mqtt.Password))
        {
            connectFlags |= 0x40;
        }

        variableHeaderAndPayload.WriteByte(connectFlags);
        WriteUInt16(
            variableHeaderAndPayload,
            (ushort)Math.Clamp(mqtt.KeepAliveSeconds, 0, ushort.MaxValue));
        WriteMqttString(variableHeaderAndPayload, ResolveSubscriberClientId(mqtt));

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

    private static string ResolveSubscriberClientId(MqttOptions mqtt)
    {
        var configured = (mqtt.RechargeSubscriberClientId ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(configured) &&
            !string.Equals(configured, mqtt.ClientId, StringComparison.Ordinal))
        {
            return configured;
        }

        var baseClientId = string.IsNullOrWhiteSpace(mqtt.ClientId)
            ? $"RfidVehicleAccess-{Environment.MachineName}"
            : (mqtt.ClientId ?? string.Empty).Trim();
        return $"{baseClientId}-recharge";
    }

    private static async Task ReadConnAckAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var packet = await ReadPacketAsync(stream, cancellationToken);
        if (packet.PacketType != 2 || packet.Payload.Length != 2)
        {
            throw new IOException("The MQTT broker returned an invalid CONNACK packet.");
        }

        if ((packet.Payload[0] & 0xFE) != 0)
        {
            throw new IOException("The MQTT broker returned invalid CONNACK flags.");
        }

        if (packet.Payload[1] != 0)
        {
            throw new IOException($"MQTT connection was rejected. Return code: {packet.Payload[1]}.");
        }
    }

    private static async Task SendSubscribeAsync(
        Stream stream,
        string topic,
        int qualityOfService,
        ushort packetIdentifier,
        CancellationToken cancellationToken)
    {
        using var payload = new MemoryStream();
        WriteUInt16(payload, packetIdentifier);
        WriteMqttString(payload, topic);
        payload.WriteByte((byte)qualityOfService);
        await WritePacketAsync(stream, 0x82, payload.ToArray(), cancellationToken);
    }

    private static async Task ReadSubAckAsync(
        Stream stream,
        ushort expectedPacketIdentifier,
        CancellationToken cancellationToken)
    {
        var packet = await ReadPacketAsync(stream, cancellationToken);
        if (packet.PacketType != 9 || packet.Payload.Length < 3)
        {
            throw new IOException("The MQTT broker returned an invalid SUBACK packet.");
        }

        var actualPacketIdentifier = BinaryPrimitives.ReadUInt16BigEndian(packet.Payload.AsSpan(0, 2));
        if (actualPacketIdentifier != expectedPacketIdentifier)
        {
            throw new IOException("The MQTT broker returned a SUBACK for an unexpected packet.");
        }

        if (packet.Payload[2] == 0x80)
        {
            throw new IOException("The MQTT broker rejected the recharge topic subscription.");
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

    private static async Task<MqttPacket> ReadPacketAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var header = await ReadByteAsync(stream, cancellationToken);
        var remainingLength = await ReadRemainingLengthAsync(stream, cancellationToken);
        if (remainingLength > MaximumPacketSize)
        {
            throw new IOException($"MQTT packet exceeds the {MaximumPacketSize}-byte limit.");
        }

        var payload = new byte[remainingLength];
        await ReadExactlyAsync(stream, payload, cancellationToken);
        return new MqttPacket(header, header >> 4, payload);
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
            var candidate = Interlocked.Increment(ref _packetIdentifier) & 0xFFFF;
            if (candidate != 0)
            {
                return (ushort)candidate;
            }
        }
    }

    private static string NormalizeRfid(string value) =>
        string.Concat((value ?? string.Empty).Where(character => !char.IsWhiteSpace(character)))
            .ToUpperInvariant();

    private static string GetUsefulErrorMessage(Exception exception)
    {
        var current = exception;
        while (current.InnerException is not null)
        {
            current = current.InnerException;
        }

        return current.Message;
    }

    private sealed record MqttPacket(byte Header, int PacketType, byte[] Payload);

    private sealed record MqttPublishPacket(
        string Topic,
        int QualityOfService,
        ushort? PacketIdentifier,
        byte[] Payload);
}
