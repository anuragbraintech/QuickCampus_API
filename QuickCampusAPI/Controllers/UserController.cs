﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuickCampus_Core.Common;
using QuickCampus_Core.Interfaces;
using QuickCampus_Core.ViewModel;
using QuickCampus_DAL.Context;

namespace QuickCampusAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IUserRepo userRepo;
        private readonly IClientRepo clientRepo;
        private IConfiguration config;
        public UserController(IUserRepo userRepo, IClientRepo clientRepo, IConfiguration config)
        {
            this.userRepo = userRepo;
            this.clientRepo = clientRepo;
            this.config = config;
        }

        [HttpPost]
        [Route("AddUser")]
        public async Task<IActionResult> AddUser(UserModel vm, int clientid)
        {
            vm.Password = EncodePasswordToBase64(vm.Password);
            IGeneralResult<UserResponseVm> result = new GeneralResult<UserResponseVm>();
            var _jwtSecretKey = config["Jwt:Key"];


            int cid = 0;
            var jwtSecretKey = config["Jwt:Key"];
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

            if (userRepo.Any(x => x.Email == vm.Email && x.IsActive == true && x.IsDelete == false))
            {
                result.Message = "Email Already Registered!";
            }
            else
            {
                if (ModelState.IsValid)
                {

                    UserVm userVm = new UserVm
                    {
                        Name = vm.Name,
                        Email = vm.Email,
                        Mobile = vm.Mobile,
                        Password = vm.Password,
                        ClientId = cid
                    };
                    var dataWithClientId = await userRepo.Add(userVm.ToUserDbModel());
                    result.IsSuccess = true;
                    result.Message = "User added successfully.";
                    result.Data = (UserResponseVm)dataWithClientId;
                    return Ok(result);
                }
                else
                {
                    result.Message = GetErrorListFromModelState.GetErrorList(ModelState);
                }
            }

            return Ok(result);

        }

        [HttpPost]
        [Route("EditUser")]
        public async Task<IActionResult> EditUser(EditUserResponseVm vm, int clientid)
        {
            IGeneralResult<EditUserResponseVm> result = new GeneralResult<EditUserResponseVm>();
            int cid = 0;
            var _jwtSecretKey = config["Jwt:Key"];
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

            if (userRepo.Any(x => x.Email == vm.Email && x.IsActive == true && x.Id != vm.Id))
            {
                result.Message = "Email Already Registered!";
            }
            else if (userRepo.Any(x => x.IsActive == true && x.Email == vm.Email.Trim()))
            {
                result.Message = "UserName Already Exist!";
            }
            else
            {
                TblUser res = new TblUser();

                if (isSuperAdmin)
                {
                    res = (await userRepo.GetAll()).Where(w=> w.Id== vm.Id && w.IsDelete==false && w.IsActive==true && (cid==0?true:w.ClientId==cid)).FirstOrDefault();
                }
                else
                {
                    res = (await userRepo.GetAll()).Where(w => w.Id == vm.Id && w.IsDelete == false && w.IsActive == true && w.ClientId == cid).FirstOrDefault();
                }
                bool isDeleted = (bool)res.IsDelete ? true : false;
                if (isDeleted)
                {
                    result.Message = " User does Not Exist";
                    return Ok(result);
                }

                if (ModelState.IsValid && vm.Id > 0 && res.IsDelete == false)
                {
                    res.Email = vm.Email;
                    res.Mobile = vm.Mobile;
                    try
                    {
                        result.Data = (EditUserResponseVm)await userRepo.Update(res);
                        result.Message = "User updated successfully";
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
            return Ok(result);
        }

        [HttpGet]
        [Route("UserList")]
        public async Task<IActionResult> UserList(int clientid)
        {

            IGeneralResult<List<UserResponseVm>> result = new GeneralResult<List<UserResponseVm>>();
            int cid = 0;
            var _jwtSecretKey = config["Jwt:Key"];
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
                List<TblUser> userlist = new List<TblUser>();
                if (isSuperAdmin)
                {
                    userlist =(await userRepo.GetAll()).ToList();
                    userlist = userlist.Where(x =>( x.IsDelete == false || x.IsDelete == null) && (cid == 0 ? true : x.ClientId == cid)).ToList();
                }
                else
                {
                    userlist = (await userRepo.GetAll()).Where(x => x.IsDelete == false && x.ClientId == cid).ToList();
                }
                if (userlist.Count > 0)
                {
                    var response = userlist.Select(x => (UserResponseVm)x).ToList();
                    result.IsSuccess = true;
                    result.Message = "User list get successfully!!";
                    result.Data = response;
                    return Ok(result);
                }
                else
                {
                    result.IsSuccess = true;
                    result.Message = "User list not found";
                    result.Data = null;
                    return Ok(result);
                }
                  
            }
            catch (Exception ex)
            {
                result.Message = ex.Message;
            }
            return Ok(result);
        }


        [HttpDelete]
        [Route("DeleteUser")]
        public async Task<IActionResult> DeleteUser(int id, int clientid, bool isDeleted)
        {
            IGeneralResult<UserVm> result = new GeneralResult<UserVm>();
            int cid = 0;
            var jwtSecretKey = config["Jwt:Key"];
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
                    result.Message = "Invalid Client";
                    return Ok(result);
                }
            }
            var res = userRepo.DeleteRole(isDeleted, id, cid, isSuperAdmin);
            return Ok(res);
        }

        [HttpGet]
        [Route("activeAndInactive")]
        public async Task<IActionResult> ActiveAndInactive(bool isActive, int id, int clientid)
        {
            IGeneralResult<UserResponseVm> result = new GeneralResult<UserResponseVm>();
            int cid = 0;
            var jwtSecretKey = config["Jwt:Key"];
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
                    result.Message = "Invalid User";
                    return Ok(result);
                }
            }

            var res = userRepo.ActiveInActiveRole(isActive, id, cid, isSuperAdmin);
            return Ok(res);
        }
        private string EncodePasswordToBase64(string password)
        {
            try
            {
                byte[] encData_byte = new byte[password.Length];
                encData_byte = System.Text.Encoding.UTF8.GetBytes(password);
                string encodedData = Convert.ToBase64String(encData_byte);
                return encodedData;
            }
            catch (Exception ex)
            {
                throw new Exception("Error in base64Encode" + ex.Message);
            }
        }
    }
}
