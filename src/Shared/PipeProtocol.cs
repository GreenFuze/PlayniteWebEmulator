using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace PlayniteWebEmulator.Protocol
{
    [DataContract]
    internal sealed class LaunchRequest
    {
        [DataMember(Name = "profileId", IsRequired = true)]
        public string ProfileId { get; set; }

        [DataMember(Name = "romPath", IsRequired = true)]
        public string RomPath { get; set; }
    }

    [DataContract]
    internal sealed class LaunchResponse
    {
        [DataMember(Name = "succeeded", IsRequired = true)]
        public bool Succeeded { get; set; }

        [DataMember(Name = "error", EmitDefaultValue = false)]
        public string Error { get; set; }

        public static LaunchResponse Success() => new LaunchResponse { Succeeded = true };

        public static LaunchResponse Failure(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                throw new ArgumentException("A failure response requires an error message.", nameof(error));
            }

            return new LaunchResponse { Succeeded = false, Error = error.Trim() };
        }
    }

    internal static class PipeProtocol
    {
        public const string PipeName = "PlayniteWebEmulator-41d5bc40-a7e8-46a6-888e-d52cf719c397";
        private const int MaximumMessageBytes = 64 * 1024;

        public static void Write<T>(Stream stream, T value)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (value == null) throw new ArgumentNullException(nameof(value));

            byte[] payload;
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var buffer = new MemoryStream())
            {
                serializer.WriteObject(buffer, value);
                payload = buffer.ToArray();
            }

            if (payload.Length == 0 || payload.Length > MaximumMessageBytes)
            {
                throw new InvalidDataException("The launch message size is invalid.");
            }

            var length = BitConverter.GetBytes(payload.Length);
            stream.Write(length, 0, length.Length);
            stream.Write(payload, 0, payload.Length);
            stream.Flush();
        }

        public static T Read<T>(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            var lengthBytes = ReadExact(stream, sizeof(int));
            var length = BitConverter.ToInt32(lengthBytes, 0);
            if (length <= 0 || length > MaximumMessageBytes)
            {
                throw new InvalidDataException("The launch message size is invalid.");
            }

            var payload = ReadExact(stream, length);
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var buffer = new MemoryStream(payload, writable: false))
            {
                var value = serializer.ReadObject(buffer);
                if (!(value is T typedValue))
                {
                    throw new SerializationException("The launch message has an unexpected type.");
                }

                return typedValue;
            }
        }

        private static byte[] ReadExact(Stream stream, int length)
        {
            var buffer = new byte[length];
            var offset = 0;
            while (offset < length)
            {
                var read = stream.Read(buffer, offset, length - offset);
                if (read == 0)
                {
                    throw new EndOfStreamException("The launch pipe closed before a complete message was received.");
                }

                offset += read;
            }

            return buffer;
        }
    }
}

