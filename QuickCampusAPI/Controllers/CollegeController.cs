﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuickCampus_Core.Common;
using QuickCampus_Core.Interfaces;
using QuickCampus_Core.ViewModel;
using Microsoft.AspNetCore.Http;
using QuickCampus_DAL.Context;
using QuickCampus_Core.Services;
using Azure;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Configuration;
using System;
using System.Net.Mail;
using System.Threading.Tasks.Dataflow;
using static QuickCampus_Core.Common.common;
using QuickCampus_Core.Common.Helper;

namespace QuickCampusAPI.Controllers
{
    [Authorize(Roles="Admin,Client,Client_User")]
    [Route("api/[controller]")]
    [ApiController]
    public class CollegeController : ControllerBase
    {
        private readonly ICollegeRepo _collegeRepo;
        private IConfiguration _config;
        private readonly ProcessUploadFile _uploadFile;
        private readonly IUserAppRoleRepo _userAppRoleRepo;
        private readonly Microsoft.AspNetCore.Hosting.IHostingEnvironment _hostingEnvironment;
        private readonly string basePath;
        private readonly ICountryRepo _countryRepo;
        private readonly IStateRepo _stateRepo;
        private readonly ICityRepo _cityRepo;
        private string baseUrl;
        private readonly BtprojecQuickcampusContext _context;
        private string _jwtSecretKey;


        public CollegeController(ICollegeRepo collegeRepo, IConfiguration config, ProcessUploadFile uploadFile,
            IUserAppRoleRepo userAppRoleRepo , Microsoft.AspNetCore.Hosting.IHostingEnvironment hostingEnvironment, ICountryRepo countryRepo, IStateRepo stateRepo, ICityRepo cityRepo, BtprojecQuickcampusContext BtprojecQuickcampusContext)
        {
            _collegeRepo = collegeRepo;
            _config = config;
            _uploadFile = uploadFile;
            _userAppRoleRepo = userAppRoleRepo;
            _hostingEnvironment = hostingEnvironment;
            _countryRepo = countryRepo;
            _stateRepo = stateRepo;
            _cityRepo = cityRepo;
            _context = BtprojecQuickcampusContext;
            _jwtSecretKey = _config["Jwt:Key"] ?? "";
            baseUrl = _config.GetSection("APISitePath").Value;

        }

        
        [HttpGet]
        [Route("GetAllCollege")]
        public async Task<IActionResult> GetAllCollege(string? search, DataTypeFilter DataType = DataTypeFilter.All, int pageStart = 1, int pageSize = 10)
        {
            IGeneralResult<List<CollegeCountryStateVmmm>> result = new GeneralResult<List<CollegeCountryStateVmmm>>();
            try
            {
                var LoggedInUserId = JwtHelper.GetIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);
                var LoggedInUserClientId = JwtHelper.GetClientIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);
                var LoggedInUserRole = (await _userAppRoleRepo.GetAll(x => x.UserId == Convert.ToInt32(LoggedInUserId))).FirstOrDefault();
                if (LoggedInUserClientId == null || LoggedInUserClientId == "0")
                {
                    LoggedInUserClientId = LoggedInUserId;
                }
                var newPageStart = 0;
                if (pageStart > 0)
                {
                    var startPage = 1;
                    newPageStart = (pageStart - startPage) * pageSize;
                }

                List<College> collegeList = new List<College>();
                List<College> collegeData = new List<College>();
                int collegeListCount = 0;
                if (LoggedInUserRole != null && LoggedInUserRole.RoleId == (int)AppRole.Admin)
                {
                    collegeData = _collegeRepo.GetAllQuerable().Where(x => x.IsDeleted == false && ((DataType == DataTypeFilter.OnlyActive ? x.IsActive == true : (DataType == DataTypeFilter.OnlyInActive ? x.IsActive == false : true)))).ToList();
                }
                else
                {
                    collegeData = _collegeRepo.GetAllQuerable().Where(x => x.ClientId == Convert.ToInt32(LoggedInUserClientId) && x.IsDeleted == false && ((DataType == DataTypeFilter.OnlyActive ? x.IsActive == true : (DataType == DataTypeFilter.OnlyInActive ? x.IsActive == false : true)))).ToList();
                }
                collegeListCount = collegeList.Count;
                collegeList = collegeData.Where(x => (x.ContectPerson.Contains(search ?? "", StringComparison.OrdinalIgnoreCase) || x.CollegeName.Contains(search ?? "", StringComparison.OrdinalIgnoreCase) || x.CollegeCode.Contains(search ?? "", StringComparison.OrdinalIgnoreCase) || x.ContectEmail.Contains(search ?? "", StringComparison.OrdinalIgnoreCase) || x.ContectPhone.Contains(search ?? ""))).OrderBy(x => x.CollegeName).ToList();
                collegeList = collegeList.Skip(newPageStart).Take(pageSize).ToList();

                var response = collegeList.Select(x => (CollegeCountryStateVmmm)x).ToList();

                if (collegeList.Count > 0)
                {
                    result.IsSuccess = true;
                    result.Message = "Data fetched successfully.";
                    result.Data = response;
                    result.TotalRecordCount = collegeListCount;
                }
                else
                {
                    result.Message = "College list not found!";
                }

            }
            catch (Exception ex)
            {
                result.Message = "Server error "  + ex.Message;
            }
            return Ok(result);
        }


        
        [HttpGet]
        [Route("GetCollegeDetailsByCollegeId")]
        public async Task<IActionResult> GetCollegeDetailsById(int collegeId)
        {
            IGeneralResult<CollegeCountryStateVmmm> result = new GeneralResult<CollegeCountryStateVmmm>();
            try
            {
                var LoggedInUserId = JwtHelper.GetIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);
                var LoggedInUserClientId = JwtHelper.GetClientIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);
                var LoggedInUserRole = (await _userAppRoleRepo.GetAll(x => x.UserId == Convert.ToInt32(LoggedInUserId))).FirstOrDefault();
                if (LoggedInUserClientId == null || LoggedInUserClientId == "0")
                {
                    LoggedInUserClientId = LoggedInUserId;
                }
                if (collegeId > 0)
                {
                    College college = new College();

                    if (LoggedInUserRole != null && LoggedInUserRole.RoleId == (int)AppRole.Admin)
                    {
                        college = _collegeRepo.GetAllQuerable().Where(x => x.CollegeId == collegeId && x.IsDeleted == false).FirstOrDefault();
                    }
                    else
                    {
                        college = _collegeRepo.GetAllQuerable().Where(x => x.CollegeId == collegeId && x.IsDeleted == false && x.ClientId == Convert.ToInt32(LoggedInUserClientId)).FirstOrDefault();
                    }
                    if (college == null)
                    {
                        result.Message = " College does Not Exist";
                    }
                    else
                    {
                        result.IsSuccess = true;
                        result.Message = "College fetched successfully.";
                        result.Data = (CollegeCountryStateVmmm)college;
                    }
                    return Ok(result);
                }
                else
                {
                    result.Message = "Please enter a valid College Id.";
                }
            }
            catch (Exception ex)
            {
                result.Message = "Server error! " + ex.Message;
            }
            return Ok(result);
        }

        [HttpPost]
        [Route("AddCollege")]
        public async Task<IActionResult> AddCollege([FromForm] AddCollegeVm vm)
        {
            IGeneralResult<CollegeVM> result = new GeneralResult<CollegeVM>();
            try
            {
                var LoggedInUserId = JwtHelper.GetIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);
                var LoggedInUserClientId = JwtHelper.GetClientIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);
                var LoggedInUserRole = (await _userAppRoleRepo.GetAll(x => x.UserId == Convert.ToInt32(LoggedInUserId))).FirstOrDefault();
                if (LoggedInUserClientId == null || LoggedInUserClientId == "0")
                {
                    LoggedInUserClientId = LoggedInUserId;
                }
                bool isCityExits = _cityRepo.Any(x => x.CityId == vm.CityId && x.IsActive == true && x.IsDeleted == false);
                if (!isCityExits)
                {
                    result.Message = " City does not exists";
                    return Ok(result);
                }
                bool isStateExits = _stateRepo.Any(x => x.StateId == vm.StateId && x.IsActive == true && x.IsDeleted == false);
                if (!isStateExits)
                {
                    result.Message = " State does not exists";
                    return Ok(result);
                }
                bool isCountryExits = _countryRepo.Any(x => x.CountryId == vm.CountryId && x.IsActive == true && x.IsDeleted == false);
                if (!isCountryExits)
                {
                    result.Message = " Country does not exists";
                    return Ok(result);
                }
                bool isNameExits = _collegeRepo.Any(x => x.CollegeName == vm.CollegeName && x.IsDeleted == false);
                if (isNameExits)
                {
                    result.Message = " College Name is already exists";
                    return Ok(result);
                }
                bool isCodeExist = _collegeRepo.Any(x => x.CollegeCode == vm.CollegeCode && x.IsDeleted == false);
                if (isCodeExist)
                {
                    result.Message = "College Code is already exist";
                    return Ok(result);
                }
                bool isContactEmailExist = _collegeRepo.Any(x => x.ContectEmail == vm.ContactEmail && x.IsDeleted == false);
                if (isContactEmailExist)
                {
                    result.Message = "Contact Email is Already Exist";
                    return Ok(result);
                }
                if (ModelState.IsValid)
                {
                    CollegeVM college = new CollegeVM
                    {
                        CollegeName = vm.CollegeName?.Trim(),
                        Address1 = vm.Address1?.Trim(),
                        Address2 = vm.Address2?.Trim(),
                        CreatedBy = Convert.ToInt32(LoggedInUserId),
                        CityId = vm.CityId,
                        StateId = vm.StateId,
                        CountryId = vm.CountryId,
                        CollegeCode = vm.CollegeCode,
                        ContectPerson = vm.ContactPerson?.Trim(),
                        ContectEmail = vm.ContactEmail?.Trim(),
                        ContectPhone = vm.ContactPhone?.Trim(),
                        ClientId = Convert.ToInt32(LoggedInUserClientId)
                    };
                    var UploadLogo = _uploadFile.GetUploadFile(vm.ImagePath);
                    if (UploadLogo.IsSuccess)
                    {
                        college.Logo = Path.Combine(baseUrl, UploadLogo.Data);
                        var addCollege = await _collegeRepo.Add(college.ToCollegeDbModel());
                        if(addCollege.CollegeId > 0)
                        {
                            result.IsSuccess = true;
                            result.Message = "College added successfully";
                            result.Data = (CollegeVM)addCollege;
                        }
                        else
                        {
                            result.Message = "Something went wrong.";
                        }
                    }
                    else
                    {
                        result.Message = UploadLogo.Message;
                        return Ok(result);
                    }
                }
                else
                {
                    result.Message = GetErrorListFromModelState.GetErrorList(ModelState);
                }
            }
            catch (Exception ex)
            {
                result.Message = ex.Message;
            }
            return Ok(result);
        }

        //[HttpPost]
        //[Route("EditCollege")]
        //public async Task<IActionResult> EditCollege([FromForm] AddCollegeVm vm)
        //{
        //    IGeneralResult<CollegeLogoVm> result = new GeneralResult<CollegeLogoVm>();
        //    try
        //    {
        //        var LoggedInUserId = JwtHelper.GetIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);
        //        var LoggedInUserClientId = JwtHelper.GetClientIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);
        //        var LoggedInUserRole = (await _userAppRoleRepo.GetAll(x => x.UserId == Convert.ToInt32(LoggedInUserId))).FirstOrDefault();
        //        if (LoggedInUserClientId == null || LoggedInUserClientId == "0")
        //        {
        //            LoggedInUserClientId = LoggedInUserId;
        //        }
        //        bool isCityExits = _cityRepo.Any(x => x.CityId == vm.CityId && x.IsActive == true && x.IsDeleted == false);
        //        if (!isCityExits)
        //        {
        //            result.Message = " City does not exists";
        //            return Ok(result);
        //        }
        //        bool isStateExits = _stateRepo.Any(x => x.StateId == vm.StateId && x.IsActive == true && x.IsDeleted == false);
        //        if (!isStateExits)
        //        {
        //            result.Message = " State does not exists";
        //            return Ok(result);
        //        }
        //        bool isCountryExits = _countryRepo.Any(x => x.CountryId == vm.CountryId && x.IsActive == true && x.IsDeleted == false);
        //        if (!isCountryExits)
        //        {
        //            result.Message = " Country does not exists";
        //            return Ok(result);
        //        }
        //        bool isCollegeNameExists = _collegeRepo.Any(x => x.CollegeName == vm.CollegeName && x.IsDeleted == false && x.CollegeId != vm.CollegeId);
        //        if (isCollegeNameExists)
        //        {
        //            result.Message = " CollegeName is already exists";
        //            return Ok(result);
        //        }
        //        bool isCollegeCodeExist = _collegeRepo.Any(x => x.CollegeCode == vm.CollegeCode && x.IsDeleted == false && x.CollegeId != vm.CollegeId);
        //        if (isCollegeCodeExist)
        //        {
        //            result.Message = "College Code is already exist";
        //            return Ok(result);
        //        }
        //        bool isContactEmailExists = _collegeRepo.Any(x => x.ContectEmail == vm.ContactEmail && x.IsDeleted == false && x.CollegeId != vm.CollegeId);
        //        if (isContactEmailExists)
        //        {
        //            result.Message = "Contact Email is Already Exist";
        //            return Ok(result);
        //        }
        //        if (ModelState.IsValid)
        //        {
        //            if(vm.CollegeId > 0)
        //            {
        //                College college = new College();

        //                if (LoggedInUserRole != null && LoggedInUserRole.RoleId == (int)AppRole.Admin)
        //                {
        //                    college = _collegeRepo.GetAllQuerable().Where(x => x.CollegeId == vm.CollegeId && x.IsDeleted == false).FirstOrDefault();
        //                }
        //                else
        //                {
        //                    college = _collegeRepo.GetAllQuerable().Where(x => x.CollegeId == vm.CollegeId && x.IsDeleted == false && x.ClientId == Convert.ToInt32(LoggedInUserClientId)).FirstOrDefault();
        //                }
        //                if (college == null)
        //                {
        //                    result.Message = " College does Not Exist";
        //                    return Ok(result);
        //                }
        //                else
        //                {
        //                    college.CollegeId = vm.CollegeId ?? 0;
        //                    college.CollegeName = vm.CollegeName?.Trim();
        //                    college.Address1 = vm.Address1?.Trim();
        //                    college.Address2 = college.Address2?.Trim();
        //                    college.ModifiedBy = Convert.ToInt32(LoggedInUserId);
        //                    college.CityId = vm.CityId;
        //                    college.StateId = vm.StateId;
        //                    college.CountryId = vm.CountryId;
        //                    college.CollegeCode = vm.CollegeCode?.Trim();
        //                    college.ContectPerson = vm.ContactPerson?.Trim();
        //                    college.ContectEmail = vm.ContactEmail?.Trim();
        //                    college.ContectPhone = vm.ContactPhone?.Trim();
        //                    college.ModifiedDate = DateTime.Now;
        //                    var UploadLogo = _uploadFile.GetUploadFile(vm.ImagePath);
        //                    if (UploadLogo.IsSuccess)
        //                    {
        //                        college.Logo = UploadLogo.Data;
        //                        await _collegeRepo.Update(college);
        //                        result.IsSuccess = true;
        //                        result.Message = "College Updated successfully.";
        //                        result.Data = vm;
        //                    }
        //                    else
        //                    {
        //                        result.Message = UploadLogo.Message;
        //                        return Ok(result);
        //                    }
        //                }
        //            }
        //            else
        //            {
        //                result.Message = "Please enter a valid College Id";
        //            }
        //        }
        //        else
        //        {
        //            result.Message = GetErrorListFromModelState.GetErrorList(ModelState);
        //            return Ok(result);
        //        }
        //    }
        //    catch(Exception ex) { 
        //            result.Message = "Server error "+ ex.Message;
        //            return Ok(result);
        //    }
        //    return Ok(result);
        //}

        [HttpDelete]
        [Route("DeleteCollege")]
        public async Task<IActionResult> DeleteCollege(int id)
        {
            IGeneralResult<CollegeVM> result = new GeneralResult<CollegeVM>();
            int cid = 0;
            var jwtSecretKey = _config["Jwt:Key"];
            var clientId = JwtHelper.GetClientIdFromToken(Request.Headers["Authorization"], jwtSecretKey);
            var isSuperAdmin = JwtHelper.isSuperAdminfromToken(Request.Headers["Authorization"], jwtSecretKey);
            if (isSuperAdmin)
            {
                cid = clientid;
            }
            else
            {
                cid = string.IsNullOrEmpty(clientId) ? 0 : Convert.ToInt32(clientId);

                if (cid == 0)
                {
                    result.IsSuccess = false;
                    result.Message = "Invalid College";
                    return Ok(result);
                }
            }
            var res = await _collegeRepo.DeleteCollege(id, cid, isSuperAdmin);
            return Ok(res);
        }

        [HttpGet]
        [Route("CollegeActiveInactive")]
        public async Task<IActionResult> ActiveAndInactive(int CollegeId)
        {
            IGeneralResult<CollegeVM> result = new GeneralResult<CollegeVM>();
            try
            {
                var LoggedInUserId = JwtHelper.GetIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);
                var LoggedInUserClientId = JwtHelper.GetClientIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);
                if (LoggedInUserClientId == null || LoggedInUserClientId == "0")
                {
                    LoggedInUserClientId = LoggedInUserId;
                }
                var LoggedInUserRole = (await _userAppRoleRepo.GetAll(x => x.UserId == Convert.ToInt32(LoggedInUserId))).FirstOrDefault();

                if (CollegeId > 0)
                {
                    College college = new College();

                    if (LoggedInUserRole != null && LoggedInUserRole.RoleId == (int)AppRole.Admin)
                    {
                        college = _collegeRepo.GetAllQuerable().Where(x => x.CollegeId == CollegeId && x.IsDeleted == false).FirstOrDefault();
                    }
                    else
                    {
                        college = _collegeRepo.GetAllQuerable().Where(x => x.CollegeId == CollegeId && x.IsDeleted == false && x.ClientId == Convert.ToInt32(LoggedInUserClientId)).FirstOrDefault();
                    }
                    if (college == null)
                    {
                        result.Message = " College does Not Exist";
                    }
                    else
                    {
                        college.IsActive = !college.IsActive;
                        college.ModifiedDate = DateTime.Now;
                        await _collegeRepo.Update(college);

                        result.IsSuccess = true;
                        result.Message = "Applicant Updated successfully.";
                        result.Data = (CollegeVM)college;
                    }
                    return Ok(result);
                }
                else
                {
                    result.Message = "Please enter a valid College Id.";
                }
            }
            catch (Exception ex)
            {
                result.Message = "Server error! " + ex.Message;
            }
            return Ok(result);
        }

        private string ProcessUploadFile([FromForm] AddCollegeVm model)
        {
            List<string> url = new List<string>();
            string uniqueFileName = null;
            if (model.ImagePath != null)
            {
                string photoUoload = Path.Combine(_hostingEnvironment.WebRootPath, "UploadFiles");
                uniqueFileName = Guid.NewGuid().ToString() + "_" + model.ImagePath.FileName;
                string filepath = Path.Combine(photoUoload, uniqueFileName);
                using (var filename = new FileStream(filepath, FileMode.Create))
                {
                    model.ImagePath.CopyTo(filename);
                }
            }

            url.Add(Path.Combine(baseUrl, uniqueFileName));
            return url.FirstOrDefault();
        }

        //private CountryTypeVm GetCountryDetails(int countryId, int userId)
        //{
        //    CountryTypeVm countryVm = new CountryTypeVm();

        //    var countryDetails = _countryRepo.GetById(countryId).Result;
        //    countryVm.CountryID = countryDetails.CountryId;
        //    countryVm.CountryName = countryDetails.CountryName;
        //    return countryVm;
        //}

        //private StateTypeVm GetstateDetails(int stateId, int userid)
        //{
        //    StateTypeVm statevm = new StateTypeVm();

        //    var stateDetails = _stateRepo.GetById(stateId).Result;
        //    statevm.StateId = stateDetails.StateId;
        //    statevm.StateName = stateDetails.StateName;

        //    return statevm;
        //}

        //private CityTypeVm GetCityDetails(int cityId, int userid)
        //{
        //    CityTypeVm cityVm = new CityTypeVm();
        //    var citydetails = _cityRepo.GetById(cityId).Result;
        //    cityVm.CityId = citydetails.CityId;
        //    cityVm.CityName = citydetails.CityName;
        //    return cityVm;
        //}

        //[HttpPost]
        //[Route("ProcessUploadFile")]
        //public List<string> ProcessUploadFile(List<IFormFile> Files)
        //{
        //    List<string> url = new List<string>();
        //    if (Files.Count > 0)
        //    {
        //        foreach (IFormFile file in Files)
        //        {
        //            string uniqueFileName = null;
        //            string photoUpload = Path.Combine(_hostingEnvironment.WebRootPath, "UploadFiles");
        //            uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
        //            string filepath = Path.Combine(photoUpload, uniqueFileName);
        //            using (var filename = new FileStream(filepath, FileMode.Create))
        //            {
        //                file.CopyTo(filename);
        //            }
        //            url.Add(Path.Combine(basePath, uniqueFileName));
        //        }
        //        return url;
        //    }
        //    else
        //    {
        //        url.Add("Please add atleast one file.");
        //        return url;
        //    }
        //}

        [Authorize(Roles = "GetAllActiveCollege")]
        [HttpGet]
        [Route("GetAllActiveCollege")]
        public async Task<IActionResult> GetAllActiveCollege(int clientid)
        {
            IGeneralResult<List<CollegeVM>> result = new GeneralResult<List<CollegeVM>>();
            int cid = 0;
            var _jwtSecretKey = _config["Jwt:Key"];
            var clientId = JwtHelper.GetClientIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);
            var isSuperAdmin = JwtHelper.isSuperAdminfromToken(Request.Headers["Authorization"], _jwtSecretKey);
            if (isSuperAdmin)
            {
                cid = clientid;
            }
            else
            {
                cid = string.IsNullOrEmpty(clientId) ? 0 : Convert.ToInt32(clientId);
            }
            try
            {

                var collegeListCount = (await _collegeRepo.GetAll()).Where(x => x.IsActive == true && x.IsDeleted == false && (cid == 0 ? true : x.CollegeId == cid)).Count();
                var collegetList = (await _collegeRepo.GetAll()).Where(x => x.IsActive == true && x.IsDeleted == false && (cid == 0 ? true : x.CollegeId == cid)).OrderByDescending(x => x.CollegeId).ToList();

                var res = collegetList.Select(x => ((CollegeVM)x)).ToList();
                if (res != null && res.Count() > 0)
                {
                    result.IsSuccess = true;
                    result.Message = "ActiveCollegeList";
                    result.Data = res;
                    result.TotalRecordCount = collegeListCount;
                }
                else
                {
                    result.Message = " Active College List Not Found";
                }
            }
            catch (Exception ex)
            {
                result.Message = ex.Message;
            }
            return Ok(result);
        }
    }
}


