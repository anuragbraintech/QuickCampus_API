using Azure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuickCampus_Core.Common;
using QuickCampus_Core.Interfaces;
using QuickCampus_Core.Services;
using QuickCampus_Core.ViewModel;
using QuickCampus_DAL.Context;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using static QuickCampus_Core.Common.common;

namespace QuickCampusAPI.Controllers
{
    [Authorize(Roles = "Admin,Client,Client_User,Admin_User")]
    [ApiController]
    [Route("api/[controller]")]
    public class RoleController : Controller
    {
        private readonly IRoleRepo _roleRepo;
        private readonly IUserRoleRepo _userRoleRepo;
        private readonly IAccount _accountRepo;
        private readonly IClientRepo _clientRepo;
        private readonly IUserRepo _userRepo;
        private IConfiguration _config;
        private readonly IUserAppRoleRepo _userAppRoleRepo;
        private string _jwtSecretKey;
        public RoleController(IRoleRepo roleRepo,IUserRoleRepo userRoleRepo, IAccount accountRepo, IClientRepo clientRepo, IUserRepo userRepo, IConfiguration config, IUserAppRoleRepo userAppRoleRepo)
        {
            _roleRepo = roleRepo;
            _userRoleRepo = userRoleRepo;
            _accountRepo = accountRepo;
            _clientRepo = clientRepo;
            _userRepo = userRepo;
            _config = config;
            _userAppRoleRepo = userAppRoleRepo;
            _jwtSecretKey = _config["Jwt:Key"] ?? "";
        }

        [HttpGet]
        [Route("RoleList")]
        
        public async Task<IActionResult> RoleList(string? search, int? ClientId, DataTypeFilter DataType, int pageStart = 1, int pageSize = 10)
        {
            IGeneralResult<List<RoleViewVm>> result = new GeneralResult<List<RoleViewVm>>();
            try
            {
                var LoggedInUserId = JwtHelper.GetIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);
                var LoggedInUserClientId = JwtHelper.GetClientIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);
                var LoggedInUserRole = (await _userAppRoleRepo.GetAll(x => x.UserId == Convert.ToInt32(LoggedInUserId))).FirstOrDefault();
                if (LoggedInUserClientId == null || LoggedInUserClientId == "0")
                {
                    var user = await _userRepo.GetById(Convert.ToInt32(LoggedInUserId));
                    LoggedInUserClientId = (user.ClientId == null ? "0": user.ClientId.ToString());
                }
                var newPageStart = 0;
                if (pageStart > 0)
                {
                    var startPage = 1;
                    newPageStart = (pageStart - startPage) * pageSize;
                }
                var rolesTotalCount = 0;
                List<TblRole> roleList = new List<TblRole>();
                List<TblRole> roleData = new List<TblRole>();
                if (LoggedInUserRole != null && LoggedInUserRole.RoleId == (int)AppRole.Admin)
                {
                    roleData = _roleRepo.GetAllQuerable().Where(x => (ClientId != null && ClientId > 0 ? x.ClientId == ClientId : true) && x.IsDeleted == false && ((DataType == DataTypeFilter.OnlyActive ? x.IsActive == true : (DataType == DataTypeFilter.OnlyInActive ? x.IsActive == false : true)))).ToList();
                }
                else
                {
                    roleData = _roleRepo.GetAllQuerable().Where(x => x.ClientId == Convert.ToInt32(LoggedInUserClientId) && x.IsDeleted == false && ((DataType == DataTypeFilter.OnlyActive ? x.IsActive == true : (DataType == DataTypeFilter.OnlyInActive ? x.IsActive == false : true)))).ToList();
                }
                if (!string.IsNullOrEmpty(search))
                {
                    search = search.Trim();
                }
                roleList = roleData.Where(x => x.Name.Contains(search ?? "", StringComparison.OrdinalIgnoreCase)).OrderByDescending(o => o.Id).ToList();
                rolesTotalCount = roleList.Count;
                roleList = roleList.Skip(newPageStart).Take(pageSize).ToList();
                if (roleList.Count > 0)
                {
                    result.IsSuccess = true;
                    result.Message = "Roles get successfully";
                    result.Data = roleList.Select(x=> (RoleViewVm)x).ToList();
                    result.TotalRecordCount = rolesTotalCount;
                }
                else
                {
                    result.Message = "No RoleData found!";
                }
            }
            catch (Exception ex)
            {
                result.Message = "server error! " + ex.Message;
            }
            return Ok(result);
        }

        [HttpPost]
        [Route("AddRole")]
        [Authorize(Roles = "AddRole")]
        public async Task<IActionResult> AddRole(RoleModel vm)
        {
            IGeneralResult<RoleViewVm> result = new GeneralResult<RoleViewVm>();
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
                    LoggedInUserClientId = (user.ClientId == null ? "0": user.ClientId.ToString());
                }
                var LoggedInUserRole = (await _userAppRoleRepo.GetAll(x => x.UserId == Convert.ToInt32(LoggedInUserId))).FirstOrDefault();


                if (_roleRepo.Any(x => x.Name == vm.RoleName))
                {
                    result.Message = "Role Already exists";
                    return Ok(result);
                }

                if (ModelState.IsValid)
                {
                    string pattern = @"^[a-zA-Z][a-zA-Z\s]*$";
                    string role = vm.RoleName ?? "";
                    Match m = Regex.Match(role, pattern, RegexOptions.IgnoreCase);
                    if (!m.Success)
                    {
                        result.Message = "Only alphabetic characters are allowed in the name.";
                        return Ok(result);
                    }
                    TblRole roleVm = new TblRole
                    {
                        Name = vm.RoleName,
                        CreatedBy = Convert.ToInt32(LoggedInUserId),
                        CreatedDate = DateTime.Now,
                        IsActive = true,
                        IsDeleted = false,
                        ClientId = (LoggedInUserRole != null && LoggedInUserRole.RoleId == (int)AppRole.Admin) ? vm.ClientId : Convert.ToInt32(LoggedInUserClientId)
                    };

                    roleVm.ClientId = LoggedInUserClientId == "0" ? null : Convert.ToInt32(LoggedInUserClientId);
                    var addRoleData = await _roleRepo.Add(roleVm);
                    if (addRoleData.Id > 0)
                    {
                        if (vm.Permission.Count > 0)
                        {
                            var addPermissions = await _roleRepo.AddRolePermissions(vm.Permission, addRoleData.Id);
                            if (!addPermissions.IsSuccess)
                            {
                                result.Message = addPermissions.Message;
                                return Ok(result);
                            }

                        }
                        result.Message = "Role added successfully.";
                        result.IsSuccess = true;
                        result.Data = (RoleViewVm)addRoleData;
                        return Ok(result);
                    }
                    else
                    {
                        result.Message = "Something went wrong.";
                    }
                }
                else
                {
                    result.Message = GetErrorListFromModelState.GetErrorList(ModelState);
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                result.Message = "Server error " + ex.Message;
            }
            return Ok(result);
        }

        [HttpPost]
        [Route("EditRole")]
        [Authorize(Roles = "EditRole")]
        public async Task<IActionResult> EditRole( RoleModel vm)
        {
            IGeneralResult<RoleViewVm> result = new GeneralResult<RoleViewVm>();
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


                if (_roleRepo.Any(x => x.Name == vm.RoleName && x.Id != vm.Id))
                {
                    result.Message = "Role Already exists";
                    return Ok(result);
                }

                if (ModelState.IsValid)
                {
                    string pattern = @"^[a-zA-Z][a-zA-Z\s]*$";
                    string role = vm.RoleName ?? "";
                    Match m = Regex.Match(role, pattern, RegexOptions.IgnoreCase);
                    if (!m.Success)
                    {
                        result.Message = "Only alphabetic characters are allowed in the name.";
                        return Ok(result);
                    }
                    var GetRole = await _roleRepo.GetById(vm.Id);
                    if (GetRole == null || GetRole.IsDeleted == true)
                    {
                        result.Message = "Invalid role.";
                        return Ok(result);
                    }

                    GetRole.Name = vm.RoleName;
                    GetRole.ModifiedBy = Convert.ToInt32(LoggedInUserId);
                    GetRole.ModifiedDate = DateTime.Now;
                    await _roleRepo.Update(GetRole);
                    if (vm.Permission.Count > 0)
                    {
                        var addPermissions = await _roleRepo.UpdateRolePermissions(vm.Permission, GetRole.Id);
                        if (!addPermissions.IsSuccess)
                        {
                            result.Message = addPermissions.Message;
                            return Ok(result);
                        }

                    }
                    result.Message = "Role updated successfully.";
                    result.IsSuccess = true;
                    result.Data = (RoleViewVm)GetRole;
                    return Ok(result);
                }
                else
                {
                    result.Message = GetErrorListFromModelState.GetErrorList(ModelState);
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                result.Message = "Server error " + ex.Message;
            }
            return Ok(result);
        }

        [Authorize(Roles = "ViewRole")]
        [HttpGet]
        [Route("GetRoleById")]
        public async Task<IActionResult> GetRoleById(int RoleId)
        {

            IGeneralResult<RoleViewVm> result = new GeneralResult<RoleViewVm>();
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

                if (RoleId > 0)
                {
                    TblRole RoleData = new TblRole();

                    if (LoggedInUserRole != null && LoggedInUserRole.RoleId == (int)AppRole.Admin)
                    {
                        RoleData = _roleRepo.GetAllQuerable().Where(x => x.Id == RoleId && x.IsDeleted == false).FirstOrDefault();
                    }
                    else
                    {
                        RoleData = _roleRepo.GetAllQuerable().Where(x => x.Id == RoleId && x.IsDeleted == false && x.ClientId == Convert.ToInt32(LoggedInUserClientId)).FirstOrDefault();
                    }
                    if (RoleData == null)
                    {
                        result.Message = " Role does not Exist";
                        return Ok(result);
                    }
                    else
                    {
                        result.Data = (RoleViewVm)RoleData;
                        result.Data.Permission = _accountRepo.GetUserPermission(RoleData.Id);
                        result.IsSuccess = true;
                        result.Message = "Role fetched successfully.";
                    }
                    return Ok(result);
                }
                else
                {
                    result.Message = "Please enter a valid Role UserId.";
                }
            }
            catch (Exception ex)
            {
                result.Message = "Server error! " + ex.Message; 
            }
            return Ok(result);
        }

        [HttpDelete]
        [Route("DeleteRoleByRoleId")]
        [Authorize(Roles = "DeleteRole")]
        public async Task<IActionResult> DeleteRole(int RoleId)
        {
            IGeneralResult<string> result = new GeneralResult<string>();
            try
            {
                var LoggedInUserId = JwtHelper.GetIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);
                var LoggedInUserClientId = JwtHelper.GetClientIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);
                if (LoggedInUserClientId == null || LoggedInUserClientId == "0")
                {
                    var user = await _userRepo.GetById(Convert.ToInt32(LoggedInUserId));
                    LoggedInUserClientId = (user.ClientId == null ? "0": user.ClientId.ToString());
                }
                var LoggedInUserRole = (await _userAppRoleRepo.GetAll(x => x.UserId == Convert.ToInt32(LoggedInUserId))).FirstOrDefault();

                if (RoleId > 0)
                {
                    TblRole RoleData = new TblRole();

                    if (LoggedInUserRole != null && LoggedInUserRole.RoleId == (int)AppRole.Admin)
                    {
                        RoleData = _roleRepo.GetAllQuerable().Where(x => x.Id == RoleId && x.IsDeleted == false).FirstOrDefault();
                    }
                    else
                    {
                        RoleData = _roleRepo.GetAllQuerable().Where(x => x.Id == RoleId && x.IsDeleted == false && x.ClientId == Convert.ToInt32(LoggedInUserClientId)).FirstOrDefault();
                    }
                    if (RoleData == null)
                    {
                        result.Message = " Role does Not Exist";
                    }
                    else
                    {
                        RoleData.IsActive = false;
                        RoleData.IsDeleted = true;
                        RoleData.ModifiedDate = DateTime.Now;
                        RoleData.ModifiedBy = Convert.ToInt32(LoggedInUserId);
                        await _roleRepo.Update(RoleData);

                        result.IsSuccess = true;
                        result.Message = "Role deleted successfully.";
                    }
                    return Ok(result);
                }
                else
                {
                    result.Message = "Please enter a valid Role UserId.";
                }
            }
            catch (Exception ex)
            {
                result.Message = "Server error! " + ex.Message;
            }
            return Ok(result);
        }

        [HttpGet]
        [Route("RoleActiveInactive")]
        [Authorize(Roles = "EditRole")]
        public async Task<IActionResult> ActiveAndInactive(int RoleId)
        {
            IGeneralResult<RoleViewVm> result = new GeneralResult<RoleViewVm>();
            try
            {
                var LoggedInUserId = JwtHelper.GetIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);
                var LoggedInUserClientId = JwtHelper.GetClientIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);
                if (LoggedInUserClientId == null || LoggedInUserClientId == "0")
                {
                    var user = await _userRepo.GetById(Convert.ToInt32(LoggedInUserId));
                    LoggedInUserClientId = (user.ClientId == null ? "0": user.ClientId.ToString());
                }
                var LoggedInUserRole = (await _userAppRoleRepo.GetAll(x => x.UserId == Convert.ToInt32(LoggedInUserId))).FirstOrDefault();

                if (RoleId > 0)
                {
                    TblRole RoleData = new TblRole();

                    if (LoggedInUserRole != null && LoggedInUserRole.RoleId == (int)AppRole.Admin)
                    {
                        RoleData = _roleRepo.GetAllQuerable().Where(x => x.Id == RoleId && x.IsDeleted == false).FirstOrDefault();
                    }
                    else
                    {
                        RoleData = _roleRepo.GetAllQuerable().Where(x => x.Id == RoleId && x.IsDeleted == false && x.ClientId == Convert.ToInt32(LoggedInUserClientId)).FirstOrDefault();
                    }
                    if (RoleData == null)
                    {
                        result.Message = " Role does Not Exist";
                    }
                    else
                    {
                        RoleData.IsActive = !RoleData.IsActive;
                        RoleData.ModifiedDate = DateTime.Now;
                        RoleData.ModifiedBy = Convert.ToInt32(LoggedInUserId);
                        await _roleRepo.Update(RoleData);

                        result.IsSuccess = true;
                        result.Message = "Role updated successfully.";
                        result.Data = (RoleViewVm)RoleData;
                    }
                    return Ok(result);
                }
                else
                {
                    result.Message = "Please enter a valid Role Id.";
                }
            }
            catch (Exception ex)
            {
                result.Message = "Server error! " + ex.Message;
            }
            return Ok(result);
        }


        //[HttpPost]
        //[Route("SetRolePermissions")]
        //public async Task<IActionResult> SetRolePermissions(RoleMappingRequest roleMappingRequest)
        //{
        //    var response = await _roleRepo.SetRolePermission(roleMappingRequest);
        //    return Ok(response);
        //}

    }
}
