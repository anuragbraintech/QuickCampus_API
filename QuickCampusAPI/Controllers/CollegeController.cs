using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using QuickCampus_Core.Common;
using QuickCampus_Core.Interfaces;
using QuickCampus_Core.ViewModel;
using System.Collections;

namespace QuickCampusAPI.Controllers
{
    public class CollegeController : Controller
    {
        private readonly ICountryRepo countryRepo;
        private readonly ICollegeRepo collegeRepo;
        public CollegeController(ICountryRepo countryRepo,ICollegeRepo collegeRepo)
        {
            this.countryRepo = countryRepo;
            this.collegeRepo = collegeRepo;
            
        }
        public IActionResult Index()
        {
            return View();
        }
        [HttpGet]
        [Route("getCollegeManage")]
        public async Task <IActionResult> Manage()
        {
            var model = new CollegeViewModel()
            {
                CollegeList = (await collegeRepo.GetAllCollege()).Select(x => new CollegeGridViewModel()
                {
                    CollegeID = x.CollegeID,
                    CollegeName = x.CollegeName,
                    Address1 = x.Address1,
                    Address2 = x.Address2,
                    City = x.City,
                    StateName = x.StateName,
                    CountryName = x.CountryName,
                    IsActive = x.IsActive,
                    CreatedDate = x.CreatedDate,
                    ContectPerson = x.ContectPerson,
                    ContectEmail = x.ContectEmail,
                    ContectPhone = x.ContectPhone
                }),
                filter = new CollegeFilter() { },
            };
                return Ok(model);
        }
    }
}
