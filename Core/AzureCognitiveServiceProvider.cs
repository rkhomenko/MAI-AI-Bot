using System;
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
                try
                {
                    await ServiceClient.AddPersonFaceAsync(GroupId, person.PersonId, imgUrl);
                }
                catch (Exception e)
                {
                    throw new Exception($"{e}\n{GroupId}\n{person}\n{imgUrl}");
                }
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
            var facesId = faces.Select(face => face.FaceId).ToArray();

            int iterCount = faces.Count() / FacesPerOnce;
            iterCount += (faces.Count() % FacesPerOnce == 0) ? 0 : 1;

            var identifyResults = new List<IdentifyResult>();
            var buffer = new Guid[FacesPerOnce];
            var residue = faces.Count();
            for (int i = 0; i < iterCount; i++) {
                var count = (residue < FacesPerOnce) ? residue : FacesPerOnce;
                Array.Copy(facesId, i * FacesPerOnce, buffer, 0, count);

                var identifyResultsArr = await ServiceClient.IdentifyAsync(GroupId, buffer.Take(count).ToArray());

                identifyResults.AddRange(identifyResultsArr);

                residue -= FacesPerOnce;
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