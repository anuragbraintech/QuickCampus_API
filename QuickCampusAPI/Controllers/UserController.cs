﻿using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuickCampus_Core.Common;
using QuickCampus_Core.Interfaces;
using QuickCampus_Core.ViewModel;
using System.Reflection.Metadata.Ecma335;

namespace QuickCampusAPI.Controllers
{
    [Authorize]
    public class UserController : Controller
    {
        private readonly IUserRepo userRepo;
        public UserController(IUserRepo userRepo)
        {
            this.userRepo = userRepo; 
            
        }
        public IActionResult Index()
        {
            return View();
        }
        [HttpPost]
        [Route("userAdd")]
        public async Task<IActionResult> addUser(UserModel vm)
        {
            IGeneralResult<UserVm> result = new GeneralResult<UserVm>();
            if (ModelState.IsValid)
            {
                UserVm userVm = new UserVm
            {
                UserName = vm.UserName,
                Name = vm.Name,
                Email = vm.Email,
                Mobile = vm.Mobile,
                Password = vm.Password,
                IsActive =true,
                IsDelete = false,
            };
                await userRepo.Add(userVm.toUserDBModel());
                result.IsSuccess = true;
                result.Message = "User added successfully.";
                result.Data = userVm;
                return Ok(result);
            }
            else
            {
                result.IsSuccess = false;
                result.Message = "something went wrong.";
            }
            return Ok(result);
        }
        [HttpGet]
        [Route("userList")]
        public async Task<IActionResult> userList()
        {
            List<UserVm> vm = new List<UserVm>();
            var list = (await userRepo.GetAll()).Where(x => x.IsDelete == false).ToList();
            vm = list.Select(x => ((UserVm)x)).ToList();
            return Ok(vm);
        }
        [HttpGet]
        [Route("userDelete")]
        public async Task<IActionResult> Delete(int id)
        {
            IGeneralResult<dynamic> result = new GeneralResult<dynamic>();
            var res = await userRepo.GetById(id);
            if (res != null)
            {
                res.IsActive = false;
                res.IsDelete = true;
                await userRepo.Update(res);
                result.IsSuccess = true;
                result.Message = "Your data is deleted successfully";
                result.Data = res;
                return Ok(result);
            }
            else
            {
                result.IsSuccess = false;
                result.Message = "something went wrong.";
            }
            return Ok(result);
        }
        [HttpPost]
        [Route("userEdit")]
        public async Task<IActionResult>Edit(int userId, UserModel vm)
        {
            IGeneralResult<UserVm> result = new GeneralResult<UserVm>();
            var res = await userRepo.GetById(userId);
            if (res != null)
            {
                res.Id = userId;
                res.UserName = vm.UserName;
                res.Name = vm.Name;
                res. Email = vm.Email;
                res.Mobile = vm.Mobile;
                res.Password = vm.Password;
                res.IsActive = true;
                res.IsDelete = false;
                await userRepo.Update(res);
                result.Message = "User data is updated successfully";
                result.IsSuccess = true;
                result.Data = (UserVm)res;
                return Ok(result);
            }
            else
            {
                result.IsSuccess = false;
                result.Message = "User data is not updated";
            }
            return Ok(result);
        }
        [HttpGet]
        [Route("activeAndInactive")]
        public async Task<IActionResult> activeAndInactive(bool IsActive,int id)
        {
            IGeneralResult<dynamic> result = new GeneralResult<dynamic>();
            if (id > 0)
            {
                var res = await userRepo.GetById(id);
                if (res != null) 
                {
                    res.IsActive = IsActive;
                    await userRepo.Update(res);
                    result.IsSuccess = true;
                    result.Message = "Your status is changed successfully";
                    result.Data = res;
                    return Ok(result);
                }
                else
                {
                    result.IsSuccess = false;
                    result.Message = "something went wrong.";
                }
            }
            return Ok(result);
        }

    }
}
