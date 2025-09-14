using AWise.AiExtensionsForGemini;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Core.Interceptors;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace SnapshotTests;

public abstract class TestBase
{
    protected const string FLASH_LITE_MODEL_NAME = $"models/gemini-2.5-flash-lite";
    protected const string PRO_MODEL_NAME = $"models/gemini-2.5-pro";

    private static bool IsRecording { get; } = GetIsRecording();

    private static bool GetIsRecording()
    {
        string? env = Environment.GetEnvironmentVariable("AWISE_AIEXTENSIONSFORGEMINI_RECORD_SNAPSHOT_TESTS");
        if (string.IsNullOrEmpty(env))
            return false;
        return env == "1" || env.ToLowerInvariant() == "true";
    }

    protected GenerativeServiceClientBuilder CreatePredictionServiceClient([CallerMemberName] string? caller = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(caller);

        string? folder = Path.GetDirectoryName(GetType().Assembly.Location);
        while (folder != null)
        {
            if (File.Exists(Path.Combine(folder, "SnapshotTests.csproj")))
                break;
            folder = Path.GetDirectoryName(folder);
        }
        if (folder is null)
        {
            throw new Exception("could not find snapshot folder");
        }
        folder = Path.Combine(folder, "grpc-request-response-snapshots", GetType().Name, caller);
        Directory.CreateDirectory(folder);

        Interceptor interceptor;
        if (IsRecording)
        {
            interceptor = new RecordingInterceptor(folder);
        }
        else
        {
            interceptor = new ReplayInterceptor(folder);
        }

        var builder = new GenerativeServiceClientBuilder()
        {
            Endpoint = $"https://generativelanguage.googleapis.com",
            Interceptor = interceptor,
            ApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY"),
        };
        if (!IsRecording)
        {
            builder.ApiKey = "fake-api";
        }
        return builder;
    }

    private static string CreateRequestPath(string folder, int ndx)
    {
        return Path.Combine(folder, $"{ndx}.request.binarypb");
    }

    private static string CreateResponsePath(string folder, int ndx)
    {
        return Path.Combine(folder, $"{ndx}.response.binarypb");
    }

    private static string CreateStatusPath(string folder, int ndx)
    {
        return Path.Combine(folder, $"{ndx}.status.json");
    }

    class RecordingInterceptor(string folder) : Interceptor
    {
        private int _count;

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context, AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
        {
            int ndx = _count++;
            string requestFile = CreateRequestPath(folder, ndx);
            string responseFile = CreateResponsePath(folder, ndx);
            string statusFile = CreateStatusPath(folder, ndx);

            using (var fs = File.Create(requestFile))
            {
                ((IMessage)request).WriteTo(fs);
            }
            var response = continuation.Invoke(request, context);
            try
            {
                var responseMessage = (IMessage)response.ResponseAsync.GetAwaiter().GetResult();
                using (var fs = File.Create(responseFile))
                {
                    responseMessage.WriteTo(fs);
                }
            }
            catch (RpcException)
            {
                // will capture the status below
            }

            using (var fs = File.Create(statusFile))
            {
                Status status = response.GetStatus();
                using var writer = new Utf8JsonWriter(fs, new JsonWriterOptions() { Indented = true });
                writer.WriteStartObject();
                writer.WriteNumber("StatusCode"u8, (int)status.StatusCode);
                writer.WriteString("Detail"u8, status.Detail);
                writer.WriteEndObject();
            }

            return response;
        }

        #region NYI
        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(ClientInterceptorContext<TRequest, TResponse> context, AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation)
        {
            throw new NotImplementedException();
        }

        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(ClientInterceptorContext<TRequest, TResponse> context, AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation)
        {
            throw new NotImplementedException();
        }

        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context, AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
        {
            throw new NotImplementedException();
        }

        public override TResponse BlockingUnaryCall<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context, BlockingUnaryCallContinuation<TRequest, TResponse> continuation)
        {
            throw new NotImplementedException();
        }

        public override Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(IAsyncStreamReader<TRequest> requestStream, ServerCallContext context, ClientStreamingServerMethod<TRequest, TResponse> continuation)
        {
            throw new NotImplementedException();
        }

        public override Task DuplexStreamingServerHandler<TRequest, TResponse>(IAsyncStreamReader<TRequest> requestStream, IServerStreamWriter<TResponse> responseStream, ServerCallContext context, DuplexStreamingServerMethod<TRequest, TResponse> continuation)
        {
            throw new NotImplementedException();
        }

        public override Task ServerStreamingServerHandler<TRequest, TResponse>(TRequest request, IServerStreamWriter<TResponse> responseStream, ServerCallContext context, ServerStreamingServerMethod<TRequest, TResponse> continuation)
        {
            throw new NotImplementedException();
        }

        public override Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
        {
            throw new NotImplementedException();
        }
        #endregion
    }

    class ReplayInterceptor(string folder) : Interceptor
    {
        private int _count;

        private static Status ReadStatusFile(string statusFile)
        {
            int statusCode;
            string detail;

            var reader = new Utf8JsonReader(File.ReadAllBytes(statusFile));
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
                throw new Exception("Missing StartObject");
            if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "StatusCode")
                throw new Exception("Missing StatusCode property");
            if (!reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out statusCode))
                throw new Exception("Failed to read StatusCode");
            if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "Detail")
                throw new Exception("Missing Detail property");
            if (!reader.Read() || reader.TokenType != JsonTokenType.String || (detail = reader.GetString()!) == null)
                throw new Exception("Failed to read Detail");
            if (!reader.Read() || reader.TokenType != JsonTokenType.EndObject)
                throw new Exception("Missing EndObject");
            if (reader.Read())
                throw new Exception("Expected EOF");

            return new Status((StatusCode)statusCode, detail);
        }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context, AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
        {
            int ndx = _count++;
            string requestFile = CreateRequestPath(folder, ndx);
            string responseFile = CreateResponsePath(folder, ndx);
            string statusFile = CreateStatusPath(folder, ndx);

            var requestSnapshot = (IMessage)Activator.CreateInstance<TRequest>();
            using (var fs = File.OpenRead(requestFile))
            {
                requestSnapshot.MergeFrom(fs);
            }
            Assert.Equal(requestSnapshot, (IMessage)request);

            Status status = ReadStatusFile(statusFile);

            Task<TResponse> responseTask;
            if (status.StatusCode == StatusCode.OK)
            {
                var response = Activator.CreateInstance<TResponse>();
                using (var fs = File.OpenRead(responseFile))
                {
                    ((IMessage)response).MergeFrom(fs);
                }
                responseTask = Task.FromResult(response);
            }
            else
            {
                responseTask = Task.FromException<TResponse>(new RpcException(status));
            }

            // TODO: maybe capture headers and trailers?
            return new AsyncUnaryCall<TResponse>(responseTask, Task.FromResult(Metadata.Empty), () => status, () => Metadata.Empty, () => { });
        }

        #region NYI
        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(ClientInterceptorContext<TRequest, TResponse> context, AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation)
        {
            throw new NotImplementedException();
        }

        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(ClientInterceptorContext<TRequest, TResponse> context, AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation)
        {
            throw new NotImplementedException();
        }

        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context, AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
        {
            throw new NotImplementedException();
        }

        public override TResponse BlockingUnaryCall<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context, BlockingUnaryCallContinuation<TRequest, TResponse> continuation)
        {
            throw new NotImplementedException();
        }

        public override Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(IAsyncStreamReader<TRequest> requestStream, ServerCallContext context, ClientStreamingServerMethod<TRequest, TResponse> continuation)
        {
            throw new NotImplementedException();
        }

        public override Task DuplexStreamingServerHandler<TRequest, TResponse>(IAsyncStreamReader<TRequest> requestStream, IServerStreamWriter<TResponse> responseStream, ServerCallContext context, DuplexStreamingServerMethod<TRequest, TResponse> continuation)
        {
            throw new NotImplementedException();
        }

        public override Task ServerStreamingServerHandler<TRequest, TResponse>(TRequest request, IServerStreamWriter<TResponse> responseStream, ServerCallContext context, ServerStreamingServerMethod<TRequest, TResponse> continuation)
        {
            throw new NotImplementedException();
        }

        public override Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
