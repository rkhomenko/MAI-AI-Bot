using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;

namespace MAIAIBot.Core
{
    public class AzureCognitiveServiceProvider : ICognitiveServiceProvider
    {
        private const int FacesPerOnce = 5;

        private const int Timeout = 1000;

        private readonly string GroupId;
        private readonly string GroupName;
        private FaceServiceClient ServiceClient;

        public AzureCognitiveServiceProvider(string groupId,
                                             string groupName,
                                             string key,
                                             string endpoint)
        {
            GroupId = groupId;
            GroupName = groupName;
            ServiceClient = new FaceServiceClient(key, endpoint);
        }

        public async Task CreateGroupIfNotExists()
        {
            try
            {
                await ServiceClient.CreatePersonGroupAsync(GroupId, GroupName);
            }
            catch (FaceAPIException)
            {
                // Group already created
                // fixme: check exception for other errors
            }
        }

        public async Task DeleteGroup()
        {
            await ServiceClient.DeletePersonGroupAsync(GroupId);
        }

        public async Task AddPerson(string personName, IEnumerable<string> imgUrls)
        {
            await CreateGroupIfNotExists();

            var person = await ServiceClient.CreatePersonAsync(GroupId, personName);
            foreach (var imgUrl in imgUrls)
            {
                await ServiceClient.AddPersonFaceAsync(GroupId, person.PersonId, imgUrl);
            }
        }

        public async Task TrainGroup()
        {
            await ServiceClient.TrainPersonGroupAsync(GroupId);
        }

        public async Task<List<IdentifyResults>> Identify(string imgUrl)
        {
            var result = new List<IdentifyResults>();

            var faces = await ServiceClient.DetectAsync(imgUrl);
            var facesId = faces.Select(face => face.FaceId);

            int facesCount = faces.Count() / FacesPerOnce;
            facesCount += (faces.Count() % FacesPerOnce == 0) ? 0 : 1;

            var identifyResults = new List<IdentifyResult>();
            for (int i = 0; i < facesCount; i++) {
                var residue = facesCount - i * FacesPerOnce;
                var count = (residue < FacesPerOnce) ? residue : FacesPerOnce;
                var identifyResultsArr = await ServiceClient.IdentifyAsync(GroupId,
                    facesId.Take(count).ToArray());

                identifyResults.AddRange(identifyResultsArr);

                Thread.Sleep(Timeout);
            }

            foreach (var identifyResult in identifyResults)
            {
                if (identifyResult.Candidates.Count() == 0)
                {
                    continue;
                }

                var resultItem = new IdentifyResults();
                foreach (var candidate in identifyResult.Candidates)
                {
                    var person = await ServiceClient.GetPersonAsync(GroupId,
                        candidate.PersonId);
                    resultItem.AddCandidate(person.Name);

                    Thread.Sleep(Timeout);
                }

                result.Add(resultItem);
            }

            return result;
        }
    }
}