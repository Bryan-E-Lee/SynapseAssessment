namespace SynapseAssessment
{
    /// <summary>
    /// Copied from https://stackoverflow.com/questions/52576394/create-default-httpclientfactory-for-integration-test as I usually don't implement this interface.
    /// </summary>
    public class DefaultHttpClientFactory : IHttpClientFactory, IDisposable
    {
        private readonly Lazy<HttpMessageHandler> _handlerLazy = new(() => new HttpClientHandler());

        public HttpClient CreateClient(string name) => new(_handlerLazy.Value, disposeHandler: false);

        public void Dispose()
        {
            if (_handlerLazy.IsValueCreated)
            {
                _handlerLazy.Value.Dispose();
            }
        }
    }
}
