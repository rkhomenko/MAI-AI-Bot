using System.Collections.Generic;
using System.Threading.Tasks;

namespace MAIAIBot.Core
{
    public class IdentifyResults
    {
        public IdentifyResults()
        {
            CandidateIds = new List<string>();
        }

        public List<string> CandidateIds { get; set; }

        public void AddCandidate(string candidateId)
        {
            CandidateIds.Add(candidateId);
        }
    }

    public interface ICognitiveServiceProvider
    {
        Task CreateGroupIfNotExists();

        Task DeleteGroup();

        Task AddPerson(string personName, IEnumerable<string> imgUrls);

        Task TrainGroup();

        Task<List<IdentifyResults>> Identify(string imgUrl);
    }
}