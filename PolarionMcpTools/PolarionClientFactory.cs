using Microsoft.Extensions.Logging; // Added for ILogger
using Polarion;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace PolarionMcpTools
{
    public interface IPolarionClientFactory
    {
        [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
        Task<IPolarionClient> CreateClientAsync();
    }

    public class PolarionClientFactory : IPolarionClientFactory
    {
        private readonly PolarionClientConfiguration _configuration;
        private readonly ILogger<PolarionClientFactory> _logger;

        public PolarionClientFactory(PolarionClientConfiguration configuration, ILogger<PolarionClientFactory> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
        public async Task<IPolarionClient> CreateClientAsync()
        {
            var clientResult = await PolarionClient.CreateAsync(_configuration);
            if (clientResult.IsFailed)
            {
                var errorMessage = clientResult.Errors.FirstOrDefault()?.Message ?? "Unknown error";
                _logger.LogError("Failed to create Polarion client via factory for server: {ServerUrl}. Error: {ErrorMessage}",
                    _configuration.ServerUrl, errorMessage);
                throw new Exception($"Failed to create Polarion client via factory: {errorMessage}");
            }

            _logger.LogInformation("Successfully created new Polarion client for server: {ServerUrl}", _configuration.ServerUrl);
            return clientResult.Value;
        }
    }
}
