using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;

namespace MAIAIBot.Core {
    public class CosmosDBProvider : IDatabaseProvider {
        private const string DB_NAME = "students";
        private const string COLLECTION_NAME = "students_collection";
        private DocumentClient Client;

        public async Task Init(string connectionStr, string key) {
            Client = new DocumentClient(new Uri(connectionStr), key);
            await Client.CreateDatabaseIfNotExistsAsync(new Database { Id = DB_NAME });
            await Client.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri(DB_NAME),
                new DocumentCollection { Id = COLLECTION_NAME }
            );
        }

        public IQueryable<Student> GetAllStudents() {
            var queryOptions = new FeedOptions { MaxItemCount = -1 };
            return Client.CreateDocumentQuery<Student>(
                UriFactory.CreateDocumentCollectionUri(
                    DB_NAME, COLLECTION_NAME),
                queryOptions
            );
        }

        public async Task AddStudent(Student student) {
            await Client.CreateDocumentAsync(
                UriFactory.CreateDocumentCollectionUri(
                    DB_NAME, COLLECTION_NAME),
                student
            );
        }

        public async Task AddStudents(IEnumerable students) {
            foreach (Student student in students) {
                await AddStudent(student);
            }
        }

        public async Task UpdateStudent(Student student) {
            await Client.ReplaceDocumentAsync(
                UriFactory.CreateDocumentUri(
                    DB_NAME, COLLECTION_NAME, student.Id),
                student
            );
        }

        public async Task UpdateStudents(IEnumerable students) {
            foreach (Student student in students) {
                await UpdateStudent(student);
            }
        }

        public async Task RemoveStudent(Student student) {
            await Client.DeleteDocumentAsync(
                UriFactory.CreateDocumentUri(
                    DB_NAME, COLLECTION_NAME, student.Id)
            );
        }

         public async Task RemoveStudents(IEnumerable students) {
            foreach (Student student in students) {
                await RemoveStudent(student);
            }
        }
    }
}