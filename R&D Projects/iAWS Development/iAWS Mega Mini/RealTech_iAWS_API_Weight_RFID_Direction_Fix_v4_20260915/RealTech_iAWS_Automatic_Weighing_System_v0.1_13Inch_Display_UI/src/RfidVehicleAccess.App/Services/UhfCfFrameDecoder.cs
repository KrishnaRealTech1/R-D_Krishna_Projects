namespace RfidVehicleAccess.Services;

/// <summary>
/// Reassembles the binary inventory stream produced by the UHF reader.
///
/// Observed inventory packet layout:
///   CF [3 bytes] [payload length] [payload] [2-byte checksum]
///
/// The payload contains the EPC length at payload offset 5, followed by the EPC bytes.
/// Serial reads may contain a partial packet, one packet, or several packets, so this
/// decoder keeps incomplete bytes until the next read and resynchronizes on 0xCF.
/// </summary>
public sealed class UhfCfFrameDecoder
{
    private const byte FrameStart = 0xCF;
    private const int HeaderLength = 5;
    private const int ChecksumLength = 2;
    private const int MinimumPayloadLength = 6;
    private const int EpcLengthOffset = 10;
    private const int EpcDataOffset = 11;
    private const int MaximumFrameLength = 512;

    private readonly object _sync = new();
    private readonly List<byte> _buffer = new(MaximumFrameLength);

    public IReadOnlyList<string> Append(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return Array.Empty<string>();
        }

        lock (_sync)
        {
            for (var index = 0; index < bytes.Length; index++)
            {
                _buffer.Add(bytes[index]);
            }

            return DecodeAvailableFrames();
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _buffer.Clear();
        }
    }

    private IReadOnlyList<string> DecodeAvailableFrames()
    {
        List<string>? epcs = null;

        while (true)
        {
            var frameStartIndex = _buffer.IndexOf(FrameStart);
            if (frameStartIndex < 0)
            {
                // No possible packet start remains. Discard noise instead of allowing
                // an unbounded buffer when a disconnected device emits garbage.
                _buffer.Clear();
                break;
            }

            if (frameStartIndex > 0)
            {
                _buffer.RemoveRange(0, frameStartIndex);
            }

            if (_buffer.Count < HeaderLength)
            {
                break;
            }

            var payloadLength = _buffer[4];
            var frameLength = HeaderLength + payloadLength + ChecksumLength;

            if (payloadLength < MinimumPayloadLength || frameLength > MaximumFrameLength)
            {
                // The 0xCF byte was noise or belonged to an unsupported packet.
                // Remove only that byte so the next 0xCF can be considered.
                _buffer.RemoveAt(0);
                continue;
            }

            if (_buffer.Count < frameLength)
            {
                break;
            }

            var payloadEndExclusive = HeaderLength + payloadLength;
            var epcLength = _buffer[EpcLengthOffset];
            var epcEndExclusive = EpcDataOffset + epcLength;

            if (epcLength == 0 || epcEndExclusive > payloadEndExclusive)
            {
                // Invalid inventory frame. Resynchronize without dropping a possible
                // valid frame that may start later in the current buffer.
                _buffer.RemoveAt(0);
                continue;
            }

            var epcBytes = _buffer.GetRange(EpcDataOffset, epcLength).ToArray();
            var epc = Convert.ToHexString(epcBytes);

            epcs ??= new List<string>();
            epcs.Add(epc);
            _buffer.RemoveRange(0, frameLength);
        }

        if (epcs is null)
        {
            return Array.Empty<string>();
        }

        return epcs;
    }
}
