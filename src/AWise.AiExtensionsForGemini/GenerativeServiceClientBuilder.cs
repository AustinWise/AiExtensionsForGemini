using Google.Ai.Generativelanguage.V1Beta;
using Google.Api.Gax;
using Google.Api.Gax.Grpc;
using Google.Protobuf.Reflection;
using Grpc.Core;
using Grpc.Core.Interceptors;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AWise.AiExtensionsForGemini;

// TODO: replace with offical implementation when available
public sealed class GenerativeServiceClientBuilder
{
    private readonly RealGenerativeServiceClientBuilder _builder;

    public GenerativeServiceClientBuilder()
    {
        _builder = new();
    }

    internal GenerativeService.GenerativeServiceClient Build()
    {
        return _builder.Build();
    }

    public string Endpoint
    {
        get => _builder.Endpoint;
        set => _builder.Endpoint = value;
    }

    public string QuotaProject
    {
        get => _builder.QuotaProject;
        set => _builder.QuotaProject = value;
    }

    public string ApiKey
    {
        get => _builder.ApiKey;
        set => _builder.ApiKey = value;
    }

    public Interceptor? Interceptor
    {
        get => _builder.Interceptor;
        set => _builder.Interceptor = value;
    }

    sealed class RealGenerativeServiceClientBuilder : ClientBuilderBase<GenerativeService.GenerativeServiceClient>
    {
        private static IEnumerable<FileDescriptor> GetFileDescriptors()
        {
            yield break;
        }
        // TODO: WithHttpRuleOverrides????
        static ApiMetadata ApiMetadata { get; } = new ApiMetadata("Google.Cloud.AI.GenerativeLanguage.V1Beta", GetFileDescriptors).WithRequestNumericEnumJsonEncoding(true);
        static string DefaultEndpoint { get; } = "generativelanguage.googleapis.com:443";
        static IReadOnlyList<string> DefaultScopes { get; } = new ReadOnlyCollection<string>(["https://www.googleapis.com/auth/cloud-platform", "https://www.googleapis.com/auth/cloud-platform.read-only", "https://www.googleapis.com/auth/generative-language"]);
        static ServiceMetadata MyServiceMetadata { get; } = new ServiceMetadata(GenerativeService.Descriptor, DefaultEndpoint, DefaultScopes, supportsScopedJwts: true, ApiTransports.Grpc | ApiTransports.Rest, ApiMetadata);
        static ChannelPool ChannelPool { get; } = new ChannelPool(MyServiceMetadata);

        internal RealGenerativeServiceClientBuilder()
            : base(MyServiceMetadata)
        {
        }

        public Interceptor? Interceptor { get; set; }

        private CallInvoker WrapCallInvoker(CallInvoker callInvoker)
        {
            if (!string.IsNullOrEmpty(ApiKey))
            {
                // TODO: there has to an easier way to get the ApiKey into the headers.
                callInvoker = new ApyKeyCallInvoker(ApiKey, callInvoker);
            }
            if (Interceptor != null)
            {
                callInvoker = callInvoker.Intercept(Interceptor);
            }
            return callInvoker;
        }

        public override GenerativeService.GenerativeServiceClient Build()
        {
            Validate();
            var invoker = WrapCallInvoker(CreateCallInvoker());
            var ret = new GenerativeService.GenerativeServiceClient(invoker);
            return ret;
        }

        public override async Task<GenerativeService.GenerativeServiceClient> BuildAsync(CancellationToken cancellationToken = default)
        {
            Validate();
            var invoker = WrapCallInvoker(await CreateCallInvokerAsync(cancellationToken));
            var ret = new GenerativeService.GenerativeServiceClient(invoker);
            return ret;
        }

        protected override ChannelPool GetChannelPool() => ChannelPool;


    }

    class ApyKeyCallInvoker(string apiKey, CallInvoker callInvoker) : CallInvoker
    {
        private CallOptions GetCallOptions(CallOptions options)
        {
            var headers = options.Headers ?? new Metadata();
            headers.Add("x-goog-api-key", apiKey);
            return options.WithHeaders(headers);
        }

        public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        {
            return callInvoker.BlockingUnaryCall(method, host, GetCallOptions(options), request);
        }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        {
            return callInvoker.AsyncUnaryCall(method, host, GetCallOptions(options), request);
        }

        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        {
            return callInvoker.AsyncServerStreamingCall(method, host, GetCallOptions(options), request);
        }

        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options)
        {
            return callInvoker.AsyncClientStreamingCall(method, host, GetCallOptions(options));
        }

        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options)
        {
            return callInvoker.AsyncDuplexStreamingCall(method, host, GetCallOptions(options));
        }
    }
}
