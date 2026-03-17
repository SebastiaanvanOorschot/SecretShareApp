using SecretShareApi.Entities;
using System.Linq;

namespace SecretShareApi.Queries.SebasDbContext
{
    public static class SharedSecretQueries
    {
        public static IQueryable<SharedSecret> FilterByHashpraseAndUrlExtension(this IQueryable<SharedSecret> query, string hashphrase, string urlExtension)
        {
            return query.Where(e => e.Token == hashphrase && e.Urlextension == urlExtension);
        }
                
        public static IQueryable<SharedSecret> FilterByUrlExtension(this IQueryable<SharedSecret> query, string urlExtension)
        {
            return query.Where(e => e.Urlextension == urlExtension);
        }
    }
}
