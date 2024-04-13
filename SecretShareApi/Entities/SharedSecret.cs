using System;

namespace SecretShareApi.Entities
{
    public class SharedSecret
    {
        public int Id { get; set; }

        public string? Secret { get; set; }
        
        public DateTime Expire { get; set; }
        
        public string? Token { get; set; }

        public string? Urlextension { get; set; }

        public int Tries { get; set; }
    }
}
