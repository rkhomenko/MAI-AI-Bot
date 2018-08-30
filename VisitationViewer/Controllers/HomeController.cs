using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using VisitationViewer.Models;

using MAIAIBot.Core;

namespace VisitationViewer.Controllers
{
    public class HomeController : Controller
    {
        private readonly IDatabaseProvider DatabaseProvider;

        public HomeController(IDatabaseProvider dabaseProvider)
        {
            DatabaseProvider = dabaseProvider;
            DatabaseProvider.Init().Wait();
        }

        public IActionResult Index()
        {
            var students = (from student in DatabaseProvider.GetAllStudents()
                            where !student.IsTeacher
                            select student).ToList();

            ViewData["Students"] = students;
            ViewData["RowCount"] = students[0].Visits.Count;

            var slides = students[0].Visits.Count / 10;
            slides += students[0].Visits.Count % 10 == 0 ? 0 : 1;
            ViewData["SlidesCount"] = slides;

            return View();
        }

        public IActionResult About()
        {
            ViewData["Message"] = "Your application description page.";

            return View();
        }

        public IActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
