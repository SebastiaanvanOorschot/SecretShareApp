namespace SecretShareApi.Controllers.DataTransferObjects
{
    public class StoreSecretDto
    {
        public string Secret { get; set; }

        public long Lifetime { get; set; }

        public string Passphrase { get; set; }
    }
}
