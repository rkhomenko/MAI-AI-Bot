using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;

namespace MAIAIBot.Core
{
    public class CosmosDBProvider : IDatabaseProvider
    {
        private const string DatabaseName = "students";
        private const string CollectionName = "students_collection";
        private DocumentClient Client;

        public CosmosDBProvider(string connectionStr, string key) {
            Client = new DocumentClient(new Uri(connectionStr), key);
        }

        public async Task Init()
        {
            await Client.CreateDatabaseIfNotExistsAsync(new Database { Id = DatabaseName });
            await Client.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri(DatabaseName),
                new DocumentCollection { Id = CollectionName }
            );
        }

        public IQueryable<Student> GetAllStudents()
        {
            var queryOptions = new FeedOptions { MaxItemCount = -1 };
            return Client.CreateDocumentQuery<Student>(
                UriFactory.CreateDocumentCollectionUri(
                    DatabaseName, CollectionName),
                queryOptions
            );
        }

        public async Task AddStudent(Student student)
        {
            await Client.CreateDocumentAsync(
                UriFactory.CreateDocumentCollectionUri(
                    DatabaseName, CollectionName),
                student
            );
        }

        public async Task AddStudents(IEnumerable students) {
            foreach (Student student in students)
            {
                await AddStudent(student);
            }
        }

        public async Task<Student> GetStudent(string id)
        {
            var response = await Client.ReadDocumentAsync(UriFactory.CreateDocumentUri(
                DatabaseName, CollectionName, id));

            return (Student)(dynamic)response.Resource;
        }

        public async Task UpdateStudent(Student student)
        {
            await Client.ReplaceDocumentAsync(
                UriFactory.CreateDocumentUri(
                    DatabaseName, CollectionName, student.Id),
                student
            );
        }

        public async Task UpdateStudents(IEnumerable students)
        {
            foreach (Student student in students)
            {
                await UpdateStudent(student);
            }
        }

        public async Task RemoveStudent(Student student)
        {
            await Client.DeleteDocumentAsync(
                UriFactory.CreateDocumentUri(
                    DatabaseName, CollectionName, student.Id)
            );
        }

         public async Task RemoveStudents(IEnumerable students)
         {
            foreach (Student student in students)
            {
                await RemoveStudent(student);
            }
        }
    }
}
