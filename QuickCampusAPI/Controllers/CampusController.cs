
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuickCampus_Core.Common;
using QuickCampus_Core.Interfaces;
using QuickCampus_Core.Services;
using QuickCampus_Core.ViewModel;
using QuickCampus_DAL.Context;
using SendGrid.Helpers.Mail;
using System.Linq.Expressions;
using System.Net.Mail;
using System.Security.Cryptography;
using static QuickCampus_Core.Common.common;

namespace QuickCampusAPI.Controllers
{
    [Authorize(Roles = "Admin,Client,Client_User")]
    [ApiController]
    [Route("api/[controller]")]
    public class CampusController : ControllerBase
    {
        private readonly ICampusRepo _campusrepo;
        private readonly ICountryRepo _country;
        private readonly IStateRepo _staterepo;
        private IConfiguration _config;
        private readonly ICityRepo _cityRepo;
        private readonly ICollegeRepo _collegeRepo;
        private readonly IUserAppRoleRepo _userAppRoleRepo;
        private readonly IUserRepo _userRepo;
        private string _jwtSecretKey;
        private ICampusWalkinCollegeRepo _campusWalkinCollegeRepo;

        public CampusController(IConfiguration configuration, ICollegeRepo collegeRepo, ICampusRepo campusrepo, ICountryRepo countryRepo, IStateRepo stateRepo, IUserAppRoleRepo userAppRoleRepo, IUserRepo userRepo, ICampusWalkinCollegeRepo campusWalkinCollegeRepo, ICityRepo cityRepo)
        {
            _campusrepo = campusrepo;
            _country = countryRepo;
            _staterepo = stateRepo;
            _config = configuration;
            _cityRepo = cityRepo;
            this._collegeRepo = collegeRepo;
            _userAppRoleRepo = userAppRoleRepo;
            _userRepo = userRepo;
            _jwtSecretKey = _config["Jwt:Key"] ?? "";
            _campusWalkinCollegeRepo = campusWalkinCollegeRepo;
        }

        [HttpGet]
        [Route("GetAllCampus")]
        public async Task<IActionResult> GetAllCampus(string? search, int? ClientId, DataTypeFilter DataType, int pageStart = 1, int pageSize = 10)
        {
            IGeneralResult<List<CampusViewModel>> result = new GeneralResult<List<CampusViewModel>>();
            try
            {
                var LoggedInUserId = JwtHelper.GetIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);
                var LoggedInUserClientId = JwtHelper.GetClientIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);
                var LoggedInUserRole = (await _userAppRoleRepo.GetAll(x => x.UserId == Convert.ToInt32(LoggedInUserId))).FirstOrDefault();
                if (LoggedInUserClientId == null || LoggedInUserClientId == "0")
                {
                    var user = await _userRepo.GetById(Convert.ToInt32(LoggedInUserId));
                    LoggedInUserClientId = (user.ClientId == null ? "0" : user.ClientId.ToString());
                }
                var newPageStart = 0;
                if (pageStart > 0)
                {
                    var startPage = 1;
                    newPageStart = (pageStart - startPage) * pageSize;
                }
                var campusTotalCount = 0;
                List<TblWalkIn> campusList = new List<TblWalkIn>();
                List<TblWalkIn> campusData = new List<TblWalkIn>();
                if (LoggedInUserRole != null && LoggedInUserRole.RoleId == (int)AppRole.Admin)
                {
                    campusData = _campusrepo.GetAllQuerable().Where(x => (ClientId != null && ClientId > 0 ? x.ClientId == ClientId : true) && x.IsDeleted == false && ((DataType == DataTypeFilter.All ? true : (DataType == DataTypeFilter.OnlyInActive ? x.IsActive == false : x.IsActive == true)))).ToList();
                }
                else
                {
                    campusData = _campusrepo.GetAllQuerable().Where(x => x.ClientId == Convert.ToInt32(LoggedInUserClientId) && x.IsDeleted == false && ((DataType == DataTypeFilter.All ? true : (DataType == DataTypeFilter.OnlyInActive ? x.IsActive == false : x.IsActive == true)))).ToList();
                }

                if (!string.IsNullOrEmpty(search))
                {
                    search = search.Trim();
                }

                campusList = campusData.Where(x => ((x.Address1 + " " + x.Address2).Contains(search ?? "", StringComparison.OrdinalIgnoreCase) || x.Title.Contains(search ?? "", StringComparison.OrdinalIgnoreCase) || x.JobDescription.Contains(search ?? "", StringComparison.OrdinalIgnoreCase) || x.Address2.Trim().Contains(search ?? "", StringComparison.OrdinalIgnoreCase))).OrderByDescending(x => x.WalkInId).ToList();

                campusTotalCount = campusList.Count;
                campusList = campusList.Skip(newPageStart).Take(pageSize).ToList();
                if (campusList.Count > 0)
                {
                    var response = campusList.Select(x => (CampusViewModel)x).ToList();
                    List<CampusViewModel> record = new List<CampusViewModel>();
                    foreach (var item in response)
                    {
                        item.CampusList = _campusWalkinCollegeRepo.GetAll(y => y.WalkInId == item.WalkInID).Result.Select(z => new CampusWalkInModel()
                        {
                            CampusId = z.CampusId,
                            StartDateTime = z.StartDateTime,
                            ExamEndTime = z.ExamEndTime.ToString(),
                            CollegeId = z.CollegeId,
                            CollegeName = (z.CollegeId != null ? _collegeRepo.GetAllQuerable().Where(x => x.CollegeId == z.CollegeId).First().CollegeName : ""),
                            CollegeCode = (z.CollegeId != null ? _collegeRepo.GetAllQuerable().Where(x => x.CollegeId == z.CollegeId).First().CollegeCode : ""),
                        }).ToList();
                    }
                    result.IsSuccess = true;
                    result.Message = "Campus get successfully";
                    result.Data = response;
                    result.TotalRecordCount = campusTotalCount;
                }
                else
                {
                    result.Message = "No Campus found!";
                }
            }
            catch (Exception ex)
            {
                result.Message = "server error! " + ex.Message;
            }
            return Ok(result);
        }

        [Authorize(Roles = "AddCampusWalkIn")]
        [HttpPost]
        [Route("AddCampus")]
        public async Task<IActionResult> AddCampus(CampusGridRequestVM vm)
        {
            IGeneralResult<CampusGridRequestVM> result = new GeneralResult<CampusGridRequestVM>();
            try
            {
                if (vm == null)
                {
                    result.Message = "Your model request is invalid.";
                    return Ok(result);
                }

                if (vm.WalkInDate < DateTime.Today)
                {
                    result.Message = "Walk-in Date must be greater than today's date.";
                    return Ok(result);
                }

                var LoggedInUserId = JwtHelper.GetIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);
                var LoggedInUserClientId = JwtHelper.GetClientIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);

                if (LoggedInUserClientId == null || LoggedInUserClientId == "0")
                {
                    var user = await _userRepo.GetById(Convert.ToInt32(LoggedInUserId));
                    LoggedInUserClientId = (user.ClientId == null ? "0" : user.ClientId.ToString());
                }

                var LoggedInUserRole = (await _userAppRoleRepo.GetAll(x => x.UserId == Convert.ToInt32(LoggedInUserId))).FirstOrDefault();
                if (LoggedInUserRole != null && LoggedInUserRole.RoleId == (int)AppRole.Admin && (vm.ClientId == null || vm.ClientId.ToString() == "" || vm.ClientId == 0))
                {
                    result.Message = "Please select a valid Client";
                    return Ok(result);
                }

                var isCountryExist = _country.GetAllQuerable().Where(w => w.IsDeleted == false).Any(a => a.CountryId == vm.CountryID);
                var allCollages = _collegeRepo.GetAllQuerable().Where(s => s.IsDeleted == false).Select(s => s.CollegeId).ToList();
                var allStates = _staterepo.GetAllQuerable().Where(w => w.IsDeleted == false && w.StateId == vm.StateID && w.CountryId == vm.CountryID).ToList();
                var isStateExist = allStates.Any(a => a.StateId == vm.StateID);
                var allCity = await _cityRepo.GetAllQuerable().Where(m => m.IsDeleted == false).Select(c => c.CityId).ToListAsync();
                var isCityExist = allCity.Any(x => x == vm.City);

                foreach (var clg in vm.Colleges)
                {
                    var checkclg = allCollages.Any(s => s == clg.CollegeId);
                    if (!checkclg)
                    {
                        result.Message = "College id " + clg.CollegeId + " does not exist";
                        return Ok(result);
                    }

                    if (!TimeSpan.TryParse(clg.ExamStartTime, out TimeSpan startTime) || !TimeSpan.TryParse(clg.ExamEndTime, out TimeSpan endTime))
                    {
                        result.Message = $"Invalid time format for College ID {clg.CollegeId}.";
                        return Ok(result);
                    }

                    if (endTime <= startTime)
                    {
                        result.Message = $"Exam end time must be greater than start time for College ID {clg.CollegeId}.";
                        return Ok(result);
                    }
                }

                if (!isCountryExist)
                {
                    result.Message = "Country does not exist";
                    return Ok(result);
                }
                else if (!isStateExist)
                {
                    result.Message = "State does not exist";
                    return Ok(result);
                }
                else if (!isCityExist)
                {
                    result.Message = "City does not exist";
                    return Ok(result);
                }

                var sv = new TblWalkIn()
                {
                    WalkInDate = vm.WalkInDate,
                    JobDescription = vm.JobDescription,
                    Address1 = vm.Address1,
                    Address2 = vm.Address2,
                    City = vm.City,
                    StateId = vm.StateID,
                    CountryId = vm.CountryID,
                    IsActive = true,
                    IsDeleted = false,
                    CreatedDate = DateTime.Now,
                    CreatedBy = Convert.ToInt32(LoggedInUserId),
                    Title = vm.Title,
                    PassingYear = vm.PassingYear,
                    ClientId = (LoggedInUserRole != null && LoggedInUserRole.RoleId == (int)AppRole.Admin)
                                ? vm.ClientId
                                : Convert.ToInt32(LoggedInUserClientId)
                };

                var walkin = await _campusrepo.Add(sv);
                vm.WalkInID = walkin.WalkInId;

                foreach (var rec in vm.Colleges)
                {
                    AddTblWalkinCollegeVm campusWalkInCollege = new AddTblWalkinCollegeVm()
                    {
                        WalkInId = sv.WalkInId,
                        CollegeId = rec.CollegeId,
                        ExamStartTime = TimeSpan.Parse(rec.ExamStartTime),
                        ExamEndTime = TimeSpan.Parse(rec.ExamEndTime),
                        CampusId = rec.CampusId,
                        StartDateTime = rec.StartDateTime,
                    };

                    var collegeWalkin = await _campusWalkinCollegeRepo.Add(campusWalkInCollege.ToDblWalinCollege());
                    rec.CampusId = collegeWalkin.CampusId;
                }

                result.IsSuccess = true;
                result.Message = "Record Saved Successfully";
                result.Data = vm;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Message = "Something went wrong";
            }
            return Ok(result);
        }

        [Authorize(Roles = "EditCampusWalkIn")]
        [HttpPost]
        [Route("UpdateCampus")]
        public async Task<IActionResult> UpdateCampus(CampusGridRequestVM vm)
        {
            IGeneralResult<CampusGridRequestVM> result = new GeneralResult<CampusGridRequestVM>();
            try
            {
                if (vm == null)
                {
                    result.Message = "Your Model request in Invalid";
                    return Ok(result);
                }
                var LoggedInUserId = JwtHelper.GetIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);
                var LoggedInUserClientId = JwtHelper.GetClientIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);
                if (LoggedInUserClientId == null || LoggedInUserClientId == "0")
                {
                    var user = await _userRepo.GetById(Convert.ToInt32(LoggedInUserId));
                    LoggedInUserClientId = (user.ClientId == null ? "0" : user.ClientId.ToString());
                }
                var LoggedInUserRole = (await _userAppRoleRepo.GetAll(x => x.UserId == Convert.ToInt32(LoggedInUserId))).FirstOrDefault();
                if (LoggedInUserRole != null && LoggedInUserRole.RoleId == (int)AppRole.Admin && (vm.ClientId == null || vm.ClientId.ToString() == "" || vm.ClientId == 0))
                {
                    result.Message = "Please select a valid Client";
                    return Ok(result);
                }
                var isCountryExist = _country.GetAllQuerable().Where(w => w.IsDeleted == false).Any(a => a.CountryId == vm.CountryID);
                var allCollages = _collegeRepo.GetAllQuerable().Where(s => s.IsDeleted == false).Select(s => s.CollegeId).ToList();
                var allStates = _staterepo.GetAllQuerable().Where(w => w.IsDeleted == false && w.StateId == vm.StateID && w.CountryId == vm.CountryID).ToList();
                var isStateExist = allStates.Any(a => a.StateId == vm.StateID);
                var allCity = await _cityRepo.GetAllQuerable().Where(m => m.IsDeleted == false).Select(c => c.CityId).ToListAsync();
                var isCityExist = allCity.Any(x => x == vm.City);

                foreach (var clg in vm.Colleges)
                {
                    var checkclg = allCollages.Any(s => s == clg.CollegeId);
                    if (!checkclg)
                    {

                        result.Message = "TblCollege id " + clg.CollegeId + " does not exist";
                        return Ok(result);
                    }
                }
                if (!isCountryExist)
                {

                    result.Message = "Country is not exist";
                    return Ok(result);
                }
                else if (!isStateExist)
                {
                    result.Message = "State is not exist";
                    return Ok(result);
                }
                else if (!isCityExist)
                {
                    result.Message = "City is Not exist ";
                    return Ok(result);
                }
                if (vm.WalkInID > 0)
                {
                    var campus = _campusrepo.GetAllQuerable().Where(x => x.WalkInId == vm.WalkInID && x.IsDeleted == false).FirstOrDefault();
                    if (campus != null)
                    {
                        campus.WalkInDate = vm.WalkInDate;
                        campus.JobDescription = vm.JobDescription;
                        campus.Address1 = vm.Address1;
                        campus.Address2 = vm.Address2;
                        campus.City = vm.City;
                        campus.StateId = vm.StateID;
                        campus.CountryId = vm.CountryID;
                        campus.Title = vm.Title;
                        campus.PassingYear = vm.PassingYear;
                        await _campusrepo.Update(campus);
                        var walkincollege = _campusWalkinCollegeRepo.GetAllQuerable().Where(x => x.WalkInId == campus.WalkInId).ToList();
                        vm.WalkInID = campus.WalkInId;
                        if (walkincollege != null)
                        {
                            foreach (var rec in walkincollege)
                            {
                                await _campusWalkinCollegeRepo.Delete(rec);
                            }
                        }
                        foreach (var rec in vm.Colleges)
                        {
                            if (rec.IsIncludeInWalkIn)
                            {
                                TblWalkInCollege campusWalkInCollege = new TblWalkInCollege()
                                {
                                    WalkInId = campus.WalkInId,
                                    CollegeId = rec.CollegeId,
                                    ExamStartTime = TimeSpan.Parse(rec.ExamStartTime),
                                    ExamEndTime = TimeSpan.Parse(rec.ExamEndTime),
                                    StartDateTime = rec.StartDateTime,
                                };
                                var updatecampus = await _campusWalkinCollegeRepo.Add(campusWalkInCollege);
                                rec.CampusId = updatecampus.CampusId;
                                
                            }
                        }
                        result.IsSuccess = true;
                        result.Message = "Record Update Successfully";
                        result.Data = vm;
                        return Ok(result);
                    }
                }
                else
                {
                    result.Message = "Something went wrong.";
                    return Ok(result);
                }
            }
            catch (Exception ex)
            {
                result.Message = "server error. " + ex.Message;
            }
            return Ok(result);
        }

        [Authorize(Roles = "ViewCampusWalkIn")]
        [HttpGet]
        [Route("GetCampusByCampusId")]
        public async Task<IActionResult> GetCampusById(int campusId)
        {
            IGeneralResult<GetCampusViewModel> result = new GeneralResult<GetCampusViewModel>();
            try
            {
                var LoggedInUserId = JwtHelper.GetIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);
                var LoggedInUserClientId = JwtHelper.GetClientIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);
                if (LoggedInUserClientId == null || LoggedInUserClientId == "0")
                {
                    var user = await _userRepo.GetById(Convert.ToInt32(LoggedInUserId));
                    LoggedInUserClientId = (user.ClientId == null ? "0" : user.ClientId.ToString());
                }
                var LoggedInUserRole = (await _userAppRoleRepo.GetAll(x => x.UserId == Convert.ToInt32(LoggedInUserId))).FirstOrDefault();
                var campusData = _campusrepo.GetAllQuerable().Where(x => x.IsDeleted == false && x.WalkInId == campusId).Include(x => x.State).Include(x => x.Country).OrderByDescending(x => x.WalkInDate).Select(x => new CampusGridViewModel()
                {
                    WalkInID = x.WalkInId,
                    Address1 = x.Address1,
                    Address2 = x.Address2,
                    City = x.City,
                    StateID = x.StateId,
                    CountryID = x.CountryId,
                    JobDescription = x.JobDescription,
                    WalkInDate = x.WalkInDate.Value,
                    IsActive = x.IsActive ?? false,
                    Title = x.Title,
                    ClientId = x.ClientId,
                    PassingYear=x.PassingYear

                }).FirstOrDefault();
              var campuswalkindata = _campusWalkinCollegeRepo.GetAllQuerable().Where(z => z.WalkInId == z.WalkInId).ToList();
                if (campuswalkindata.Count > 0)
                {
                    campusData.Colleges = campuswalkindata.Select(y => new CampusWalkInModel()
                    {
                        CampusId = y.CampusId,
                        CollegeId = y.CollegeId ?? 0,
                        ExamEndTime = y.ExamEndTime.Value.ToString(),
                        ExamStartTime = y.ExamStartTime.Value.ToString(),
                        IsIncludeInWalkIn = true,
                        StartDateTime = y.StartDateTime.Value,
                        CollegeName = _collegeRepo.GetAllQuerable().Where(z => z.CollegeId == y.CollegeId).First().CollegeName,
                        CollegeCode = _collegeRepo.GetAllQuerable().Where(z => z.CollegeId == y.CollegeId).First().CollegeCode,
                    }).ToList();
                  }
                if (campusData != null)
                {
                    GetCampusViewModel vmm = new GetCampusViewModel
                    {
                        WalkInID = campusData.WalkInID,
                        Address1 = campusData.Address1,
                        Address2 = campusData.Address2,
                        City = campusData.City,
                        StateID = campusData.StateID,
                        CountryID = campusData.CountryID,
                        IsActive = campusData.IsActive,
                        Colleges = campusData?.Colleges,
                        JobDescription = campusData.JobDescription,
                        Title = campusData?.Title,
                        WalkInDate = campusData.WalkInDate,
                        ClientId = campusData.ClientId,
                        PassingYear=campusData.PassingYear
                    };
                    result.Data = vmm;
                    result.IsSuccess = true;
                    result.Message = "Campus fetched Successfully";
                }
                else
                {
                    result.Message = "Data Not found";
                    return Ok(result);
                }
            }
            catch (Exception ex)
            {
                result.Message = "Server error! " + ex.Message;
            }
            return Ok(result);
        }

        [Authorize(Roles = "EditCampusWalkIn")]
        [HttpGet]
        [Route("CampusActiveInActive")]
        public async Task<IActionResult> ActiveInActive(int campusId, bool status)
        {
            IGeneralResult<CampusViewModel> result = new GeneralResult<CampusViewModel>();
            try
            {
                var _jwtSecretKey = _config["Jwt:Key"];
                var LoggedInUserId = JwtHelper.GetIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);
                var LoggedInUserRole = (await _userAppRoleRepo.GetAll(x => x.UserId == Convert.ToInt32(LoggedInUserId))).FirstOrDefault();
                if (LoggedInUserRole != null && LoggedInUserRole.RoleId == (int)AppRole.Admin)
                {
                    var res = _campusrepo.GetAllQuerable().Where(x => x.WalkInId == campusId && x.IsDeleted == false).FirstOrDefault();
                    int campusTotalCount = 0;
                    campusTotalCount = _campusrepo.GetAllQuerable().Where(x => x.WalkInId == campusId && x.IsDeleted == false).Count();
                    if (res != null)
                    {
                        res.IsActive = !res.IsActive;
                        var data = await _campusrepo.Update(res);
                        result.Data = (CampusViewModel)data;
                        result.IsSuccess = true;
                        result.TotalRecordCount = campusTotalCount;
                        result.Message = "Campus status changed successfully";
                    }
                }
                else
                {
                    result.Message = "Access Denied";
                }
            }
            catch (Exception ex)
            {
                result.Message = "Server Error " + ex.Message;
            }
            return Ok(result);
        }

        [Authorize(Roles = "DeleteCampusWalkIn")]
        [HttpDelete]
        [Route("DeleteCampus")]
        public async Task<IActionResult> DeleteCampus(int campusId)
        {
            IGeneralResult<CampusGridViewModel> result = new GeneralResult<CampusGridViewModel>();
            try
            {
                var _jwtSecretKey = _config["Jwt:Key"];
                var LoggedInUserId = JwtHelper.GetIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);
                var LoggedInUserClientId = JwtHelper.GetClientIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);
                if (LoggedInUserClientId == null || LoggedInUserClientId == "0")
                {
                    var user = await _userRepo.GetById(Convert.ToInt32(LoggedInUserId));
                    LoggedInUserClientId = (user.ClientId == null ? "0" : user.ClientId.ToString());
                }
                var res = await _campusrepo.DeleteCampus(campusId);
                return Ok(res);
            }
            catch (Exception ex)
            {
                result.Message = "Server Error" + ex.Message;
            }
            return Ok(result);
        }
    }
}
