using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Dynamic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NeoSmart.StjTest
{
    [TestClass]
    public class StateResumption
    {
        private readonly static Encoding DefaultEncoding = new UTF8Encoding(false);

        public struct Payload
        {
            public string Field1 { get; set; }
            public string Field2 { get; set; }
        }

        private byte[] GetRawJson()
        {
            return JsonSerializer.ToUtf8Bytes(new
            {
                success = true,
                payload = new Payload
                {
                    Field1 = "hello",
                    Field2 = "goodbye",
                }
            });
        }

        readonly ref struct JsonNode
        {
            public readonly JsonTokenType TokenType;
            public readonly ReadOnlySpan<byte> Value;

            public JsonNode(JsonTokenType tokenType, ReadOnlySpan<byte> value)
            {
                TokenType = tokenType;
                Value = value;
            }
        }

        private bool GetProperty(string property, ref Utf8JsonReader reader, out JsonNode node)
        {
            var utf8PropertyName = DefaultEncoding.GetBytes(property);

            bool keyFound = false;
            while (reader.Read())
            {
                if (!keyFound && reader.TokenType == JsonTokenType.StartObject)
                {
                    continue;
                }

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.ValueSpan;
                    if (propertyName.SequenceEqual(utf8PropertyName))
                    {
                        keyFound = true;
                    }
                    continue;
                }

                if (keyFound)
                {
                    node = new JsonNode(reader.TokenType, reader.ValueSpan);
                    return true;
                }
            }

            node = default;
            return false;
        }

        private bool? GetSuccess(ref Utf8JsonReader reader, ref JsonReaderState state)
        {
            if (GetProperty("success", ref reader, out var node))
            {
                return node.TokenType == JsonTokenType.True;
            }

            return null;
        }

        private Payload? GetPayload(ref Utf8JsonReader reader, ref JsonReaderState state)
        {
            if (GetProperty("payload", ref reader, out var node))
            {
                return JsonSerializer.ReadValue<Payload>(ref reader);
            }

            return null;
        }

        [TestMethod]
        public void ResumeWithReader()
        {
            var json = GetRawJson();

            var state = new JsonReaderState(new JsonReaderOptions() { MaxDepth = 2 });
            var reader = new Utf8JsonReader(json, false, state);

            var success = GetSuccess(ref reader, ref state);
            Assert.AreEqual(true, success);

            var payload = GetPayload(ref reader, ref state);
            Assert.AreEqual("hello", payload?.Field1);
            Assert.AreEqual("goodbye", payload?.Field2);
        }

        [TestMethod]
        public void ResumeWithState()
        {
            var json = GetRawJson();

            var state = new JsonReaderState(new JsonReaderOptions() { MaxDepth = 2 });
            var reader = new Utf8JsonReader(json, false, state);

            var success = GetSuccess(ref reader, ref state);
            Assert.AreEqual(true, success);

            // Create a new reader to continue where the first one left off
            state = reader.CurrentState;
            reader = new Utf8JsonReader(json, false, state);

            var payload = GetPayload(ref reader, ref state);
            Assert.AreEqual("hello", payload?.Field1);
            Assert.AreEqual("goodbye", payload?.Field2);
        }
    }
}
