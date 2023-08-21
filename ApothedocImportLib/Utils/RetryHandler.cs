using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApothedocImportLib.Utils
{
    public class RetryHandler : DelegatingHandler
    {
        private const int maxRetryAttempts = 5;

        public RetryHandler(HttpMessageHandler innerHandler) : base(innerHandler) { }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = null;

            for (int retry = 0; retry < maxRetryAttempts; retry++)
            {
                try
                {
                    response = await base.SendAsync(request, cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        return response;
                    }
                }
                catch (HttpRequestException ex)
                {
                    Log.Warning($">>> API server connection failed for {request.RequestUri}. Retrying operation (current try: {retry + 1})");
                }

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }

            return response;
        }

    }
}
