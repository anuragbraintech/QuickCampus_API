﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuickCampus_Core.Common;
using QuickCampus_Core.Common.Enum;
using QuickCampus_Core.Interfaces;
using QuickCampus_Core.Services;
using QuickCampus_Core.ViewModel;
using QuickCampus_DAL.Context;

namespace QuickCampusAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]

    public class ApplicantController : ControllerBase
    {

        private readonly IApplicantRepo _applicantRepo;
        private readonly IConfiguration _config;

        public ApplicantController(IConfiguration configuration, IApplicantRepo applicantRepo)
        {
            _applicantRepo = applicantRepo;
            _config = configuration;
        }

        //[AllowAnonymous]
        [HttpGet]
        [Route("GetAllApplicant")]
        public async Task<ActionResult> GetAllApplicant(int clientid)
        {
            IGeneralResult<List<ApplicantViewModel>> result = new GeneralResult<List<ApplicantViewModel>>();
            var _jwtSecretKey = _config["Jwt:Key"];

            int cid = 0;
            var jwtSecretKey = _config["Jwt:Key"];
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
            List<Applicant> countrylist = new List<Applicant>();
            try
            {
                if (isSuperAdmin)
                {
                    countrylist = (await _applicantRepo.GetAll()).Where(x => x.IsDeleted != true && (cid == 0 ? true : x.ClientId == cid)).ToList();
                }
                else
                {
                    countrylist = (await _applicantRepo.GetAll()).Where(x => x.IsDeleted != true && x.ClientId == cid).ToList();
                }
                var response = countrylist.Select(x => (ApplicantViewModel)x).ToList();
                if (countrylist.Count > 0)
                {
                    result.IsSuccess = true;
                    result.Message = "Applicant get successfully";
                    result.Data = response;
                }
                else
                {
                    result.IsSuccess = true;
                    result.Message = "Applicant list not found!";
                    result.Data = null;
                }
            }
            catch (Exception ex)
            {
                result.Message = ex.Message;
            }
            return Ok(result);
        }

        [HttpPost]
        [Route("EditApplicant")]

        public async Task<IActionResult> EditApplicant(ApplicantViewModel vm, int clientid)
        {
            IGeneralResult<ApplicantViewModel> result = new GeneralResult<ApplicantViewModel>();
            var _jwtSecretKey = _config["Jwt:Key"];
            var userId = JwtHelper.GetIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);

            int cid = 0;
            var clientId = JwtHelper.GetClientIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);
            var isSuperAdmin = JwtHelper.isSuperAdminfromToken(Request.Headers["Authorization"], _jwtSecretKey);
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
                    result.Message = "Invalid Client";
                    return Ok(result);
                }
            }
            if (vm != null)
            {
                Applicant applicant = new Applicant();

                if (isSuperAdmin)
                {
                    applicant = (await _applicantRepo.GetAll()).Where(w => w.IsDeleted == false && w.IsActive == true && cid == 0 ? true : w.ClientId == cid).FirstOrDefault();
                }
                else
                {
                    applicant = (await _applicantRepo.GetAll()).Where(w => w.IsDeleted == false && w.IsActive == true && w.ClientId == cid).FirstOrDefault();
                }
                if (applicant == null)
                {
                    result.IsSuccess = false;
                    result.Message = " Applicant does Not Exist";
                    return Ok(result);
                }
                bool isDeleted = (bool)applicant.IsDeleted ? true : false;
                if (isDeleted)
                {
                    result.IsSuccess = false;
                    result.Message = " Applicant does Not Exist";
                    return Ok(result);
                }
                bool isExits = _applicantRepo.Any(x => x.FirstName == vm.FirstName && x.IsDeleted == false);
                if (isExits)
                {
                    result.Message = " FirstName is already exists";
                }
               
                bool isphonenumberexist = _applicantRepo.Any(x => x.PhoneNumber == vm.PhoneNumber && x.IsDeleted == false);
                if (isphonenumberexist)
                {
                    result.Message = "Phone Number  is Already Exist";
                }
                bool isemailAddress = _applicantRepo.Any(x => x.EmailAddress == vm.EmailAddress && x.IsDeleted == false);
                if (isemailAddress)
                {
                    result.Message = "Email Address is Already Exist";
                }
                else
                {
                    if (ModelState.IsValid && vm.ApplicantID > 0 && applicant.IsDeleted == false)
                    {
                        applicant.ApplicantId = vm.ApplicantID;
                        applicant.CollegeName = vm.CollegeName.Trim();
                        applicant.FirstName = vm.FirstName.Trim();
                        applicant.LastName = vm.LastName.Trim();
                        applicant.EmailAddress = vm.EmailAddress.Trim();
                        applicant.HigestQualification = vm.HigestQualification.Trim();
                        applicant.IntermediatePercentage = vm.MatricPercentage;
                        applicant.ClientId = cid;
                        applicant.HigestQualificationPercentage = vm.HigestQualificationPercentage;
                        applicant.Skills = vm.Skills;
                        applicant.MatricPercentage=vm.MatricPercentage;
                        applicant.PhoneNumber = vm.PhoneNumber;
                        applicant.StatusId = (int)(StatusEnum)vm.StatusId;
                        applicant.AssignedToCompany = vm.AssignedToCompany;
                        applicant.CollegeId = vm.CollegeId;
                        applicant.Comment = vm.Comment;

                        try
                        {
                            result.Data = (ApplicantViewModel)await _applicantRepo.Update(applicant);
                            result.Message = "Applicant updated successfully";
                            result.IsSuccess = true;
                        }
                        catch (Exception ex)
                        {
                            result.Message = ex.Message;
                        }
                        return Ok(result);
                    }
                    else
                    {
                        result.Message = "something Went Wrong";
                    }
                }
            }
            return Ok(result);
        }

        [HttpGet]
        [Route("GetApplicantById")]
        public async Task<ActionResult> GetApplicantById(int applicantId,int clientid)
        {
            IGeneralResult<ApplicantViewModel> result = new GeneralResult<ApplicantViewModel>();
            var _jwtSecretKey = _config["Jwt:Key"];
            int cid = 0;
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

            var res = await _applicantRepo.GetById(applicantId);
            if (res.IsDeleted == false && res.IsActive == true)
            {
                result.Data = (ApplicantViewModel)res;
                result.IsSuccess = true;
                result.Message = "College details getting succesfully";
            }
            else
            {
                result.Message = "College does Not exist";
            }
            return Ok(result);
        }


        [HttpDelete]
        [Route("DeleteApplicant")]
        public async Task<IActionResult> DeleteApplicant(int ApplicantId)
        {
            var _jwtSecretKey = _config["Jwt:Key"];
            var clientId = JwtHelper.GetClientIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);
            IGeneralResult<CollegeVM> result = new GeneralResult<CollegeVM>();
            var res = await _applicantRepo.GetById(ApplicantId);
            if (res.IsDeleted == false)
            {
                res.IsActive = false;
                res.IsDeleted = true;
                await _applicantRepo.Update(res);
                result.IsSuccess = true;
                result.Message = "Applicant Deleted Succesfully";
            }
            else
            {
                result.Message = "Applicant does Not exist";
            }
            return Ok(result);
        }

    }
}

