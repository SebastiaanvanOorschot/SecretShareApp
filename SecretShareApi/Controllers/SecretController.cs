using Microsoft.AspNetCore.Mvc;
using SecretShareApi.Controllers.DataTransferObjects;
using SecretShareApi.Repositories;

namespace SecretShareApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SecretController : ControllerBase
    {
        private readonly SharedSecretRepository _sharedSecretRepository;
        
        public SecretController(SharedSecretRepository sharedSecretRepository)
        {
            _sharedSecretRepository = sharedSecretRepository;
        }
        
        [HttpPost]
        [Route("Store")]
        public async Task<JsonResult> Store(StoreSecretDto dto)
        {
            var url = await _sharedSecretRepository.EncryptAndSaveSecret(dto);

            return new JsonResult(url);
        }

        [HttpPost]
        [Route("Retrieve")]
        public async Task<IActionResult> Retrieve(RetrieveSecretDto dto)
        {
            var secret = await _sharedSecretRepository.VerifyAndDecryptSecret(dto);

            if (secret == "")
            {
                return NotFound();
            }

            return new JsonResult(secret);
        }
    }
}

