using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;

namespace MAIAIBot.Core {
    public interface IDatabaseProvider
    {
        IQueryable<Student> GetAllStudents();

        Task AddStudent(Student student);

        Task AddStudents(IEnumerable students);

        Task UpdateStudent(Student student);

        Task UpdateStudents(IEnumerable students);

        Task RemoveStudent(Student student);

        Task RemoveStudents(IEnumerable students);
    }
}
