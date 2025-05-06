using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuickCampus_Core.Common;
using QuickCampus_Core.Common.Helper;
using QuickCampus_Core.Interfaces;
using QuickCampus_Core.Services;
using QuickCampus_Core.ViewModel;
using QuickCampus_DAL.Context;
using static QuickCampus_Core.Common.common;

namespace QuickCampusAPI.Controllers
{
    [Authorize(Roles = "Admin,Client,Client_User")]
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly IUserRepo _userRepo;
        private readonly IClientRepo _clientRepo;
        private readonly IUserAppRoleRepo _userAppRoleRepo;
        private IConfiguration _config;
        private string _jwtSecretKey;
        private readonly IUserRoleRepo _UserRoleRepo;
        private readonly IRoleRepo _roleRepo;
        private readonly ProcessUploadFile _uploadFile;
        private string baseUrl;


        public UserController(IUserRepo userRepo, IClientRepo clientRepo, IUserAppRoleRepo userAppRoleRepo, IConfiguration config, IUserRoleRepo userRoleRepo, IRoleRepo roleRepo, ProcessUploadFile uploadFile)
        {
            _userRepo = userRepo;
            _clientRepo = clientRepo;
            _userAppRoleRepo = userAppRoleRepo;
            _config = config;
            _jwtSecretKey = _config["Jwt:Key"] ?? "";
            _UserRoleRepo = userRoleRepo;
            _roleRepo = roleRepo;
            _uploadFile = uploadFile;
            baseUrl = _config.GetSection("APISitePath").Value;
        }

        [HttpGet]
        [Route("GetAllUser")]
        public async Task<IActionResult> UserList(string? search, int? ClientId, DataTypeFilter DataType, int pageStart = 1, int pageSize = 10)
        {
            IGeneralResult<List<UserViewVm>> result = new GeneralResult<List<UserViewVm>>();
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

                List<TblUser> userList = new List<TblUser>();
                List<TblUser> userData = new List<TblUser>();
                int userListCount = 0;
                if (LoggedInUserRole != null && LoggedInUserRole.RoleId == (int)AppRole.Admin)
                {
                    userData = _userRepo.GetAllQuerable().Where(x => (ClientId != null && ClientId > 0 ? x.ClientId == ClientId : true) && x.IsDelete == false && ((DataType == DataTypeFilter.OnlyActive ? x.IsActive == true : (DataType == DataTypeFilter.OnlyInActive ? x.IsActive == false : true)))).ToList();
                }
                else
                {
                    userData = _userRepo.GetAllQuerable().Where(x => x.ClientId == Convert.ToInt32(LoggedInUserClientId) && x.IsDelete == false && ((DataType == DataTypeFilter.OnlyActive ? x.IsActive == true : (DataType == DataTypeFilter.OnlyInActive ? x.IsActive == false : true)))).ToList();
                }
                if (!string.IsNullOrEmpty(search))
                {
                    search = search.Trim();
                }
                userList = userData.Where(x => (x.Email.Contains(search ?? "", StringComparison.OrdinalIgnoreCase) || x.Name.Contains(search ?? "", StringComparison.OrdinalIgnoreCase) || x.Mobile.Contains(search ?? "", StringComparison.OrdinalIgnoreCase))).OrderBy(x => x.Name).ToList();
                userListCount = userList.Count;
                userList = userList.Skip(newPageStart).Take(pageSize).ToList();

                var response = userList.Select(x => (UserViewVm)x).ToList();

                if (userList.Count > 0)
                {
                    result.IsSuccess = true;
                    result.Message = "Data fetched successfully.";
                    result.Data = response;
                    result.TotalRecordCount = userListCount;
                }
                else
                {
                    result.Message = "No user found!";
                }

            }
            catch (Exception ex)
            {
                result.Message = "Server error " + ex.Message;
            }
            return Ok(result);
        }

        [Authorize(Roles = "AddUser")]
        [HttpPost]
        [Route("AddUser")]
        public async Task<IActionResult> AddUser(UserModel vm)
        {
            IGeneralResult<UserViewVm> result = new GeneralResult<UserViewVm>();
            try
            {
                if (vm == null)
                {
                    result.Message = "Your Model request in Invalid";
                    return Ok(result);
                }
                var LoggedInUserId = JwtHelper.GetIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);
                var LoggedInUserClientId = JwtHelper.GetClientIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);
                var LoggedInUserRole = (await _userAppRoleRepo.GetAll(x => x.UserId == Convert.ToInt32(LoggedInUserId))).FirstOrDefault();
                if (LoggedInUserClientId == null || LoggedInUserClientId == "0")
                {
                    var user = await _userRepo.GetById(Convert.ToInt32(LoggedInUserId));
                    LoggedInUserClientId = (user.ClientId == null ? "0" : user.ClientId.ToString());
                }
                vm.Password = EncodePasswordToBase64(vm.Password ?? "");

                if (_userRepo.Any(x => x.Email == vm.Email && x.IsActive == true && x.IsDelete == false))
                {
                    result.Message = "Email already Exists";
                    return Ok(result);
                }
                if(!_roleRepo.Any(x=>x.Id==vm.RoleId && x.IsDeleted==false && x.IsActive == true))
                {
                    result.Message = "Invalid Role";
                    return Ok(result);
                }
                if (vm.ImagePath != null)
                {
                    var ChecKImg = _uploadFile.CheckImage(vm.ImagePath);
                    if (!ChecKImg.IsSuccess)
                    {
                        result.Message = ChecKImg.Message;
                        return Ok(result);
                    }
                }
                if (ModelState.IsValid)
                {
                    TblUser userVm = new TblUser
                    {
                        Name = vm.Name?.Trim(),
                        Email = vm.Email?.Trim(),
                        Mobile = vm.Mobile?.Trim(),
                        Password = vm.Password.Trim(),
                        CreateDate = DateTime.Now,
                        ClientId = (LoggedInUserRole != null && LoggedInUserRole.RoleId == (int)AppRole.Admin) ? ((vm.ClientId == 0 || vm.ClientId == null) ? null : vm.ClientId) : Convert.ToInt32(LoggedInUserClientId)
                    };
                    var UploadPicture = _uploadFile.GetUploadFile(vm.ImagePath);
                    if (UploadPicture.IsSuccess)
                    {
                        userVm.ProfilePicture = UploadPicture.Data;

                    }
                    var addUser = await _userRepo.Add(userVm);
                    
                    if (addUser.Id > 0)
                    {

                        IGeneralResult<string> addRole = new GeneralResult<string>();
                        if (LoggedInUserRole.RoleId == (int)AppRole.Admin && (vm.ClientId == 0 || vm.ClientId == null))
                        {
                            addRole = await AddorUpdateRole(addUser.Id, AppRole.Admin_User, vm.RoleId);
                        }
                        else
                        {
                            addRole = await AddorUpdateRole(addUser.Id, AppRole.Client_User, vm.RoleId);
                        }
                        if (addRole.IsSuccess)
                        {

                            result.IsSuccess = true;
                            result.Message = "User added successfully.";
                            result.Data = (UserViewVm)addUser;
                            result.Data.ProfilePicture = Path.Combine(baseUrl, UploadPicture.Data ?? "");

                            return Ok(result);
                        }
                        result.Message = addRole.Message;
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
            }
            catch (Exception ex)
            {
                result.Message = "Server error. " + ex.Message;
            }
            return Ok(result);

        }

        [Authorize(Roles = "EditUser")]
        [HttpPost]
        [Route("EditUser")]
        public async Task<IActionResult> EditUser(UserRequestVm vm)
        {

            IGeneralResult<UserViewVm> result = new GeneralResult<UserViewVm>();
            try
            {
                if (vm == null)
                {
                    result.Message = "Your Model request in Invalid";
                    return Ok(result);
                }
                var LoggedInUserId = JwtHelper.GetIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);
                var LoggedInUserClientId = JwtHelper.GetClientIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);
                var LoggedInUserRole = (await _userAppRoleRepo.GetAll(x => x.UserId == Convert.ToInt32(LoggedInUserId))).FirstOrDefault();
                if (LoggedInUserClientId == null || LoggedInUserClientId == "0")
                {
                    var user = await _userRepo.GetById(Convert.ToInt32(LoggedInUserId));
                    LoggedInUserClientId = (user.ClientId == null ? "0" : user.ClientId.ToString());
                }
                //vm.Password = EncodePasswordToBase64(vm.Password ?? "");

                if (vm.UserId != null && vm.UserId > 0)
                {
                    if (_userRepo.Any(x => x.Email == vm.Email && x.IsActive == true && x.IsDelete == false && x.Id != vm.UserId))
                    {
                        result.Message = "Email already Exists";
                        return Ok(result);
                    }
                    if (_userRepo.Any(x => x.Mobile == vm.Mobile && x.IsActive == true && x.IsDelete == false && x.Id != vm.UserId))
                    {
                        result.Message = "Mobile already Exists";
                        return Ok(result);
                    }
                    if (!_roleRepo.Any(x => x.Id == vm.RoleId && x.IsDeleted == false && x.IsActive == true))
                    {
                        result.Message = "Invalid Role";
                        return Ok(result);
                    }
                    if (ModelState.IsValid)
                    {
                        TblUser user = new TblUser();

                        if (LoggedInUserRole != null && LoggedInUserRole.RoleId == (int)AppRole.Admin)
                        {
                            user = _userRepo.GetAllQuerable().Where(x => x.Id == vm.UserId && x.IsDelete == false).FirstOrDefault();
                        }
                        else
                        {
                            user = _userRepo.GetAllQuerable().Where(x => x.Id == vm.UserId && x.IsDelete == false && x.ClientId == Convert.ToInt32(LoggedInUserClientId)).FirstOrDefault();
                        }
                        if (user == null)
                        {
                            result.Message = " User does Not Exist";
                            return Ok(result);
                        }

                        user.Name = vm.Name;
                        user.Mobile = vm.Mobile;
                        user.Email = vm.Email;
                        user.ModifiedDate = DateTime.Now;
                        var updateUser = await _userRepo.Update(user);
                        IGeneralResult<string> addRole = new GeneralResult<string>();
                            addRole = await AddorUpdateRole(user.Id, AppRole.None, vm.RoleId);
                        result.IsSuccess = true;
                        result.Message = "User updated successfully.";
                        result.Data = (UserViewVm)updateUser;
                        return Ok(result);
                    }
                    else
                    {
                        result.Message = GetErrorListFromModelState.GetErrorList(ModelState);
                    }
                }
                else
                {
                    result.Message = "Please enter a valid User UserId.";
                }
            }
            catch (Exception ex)
            {
                result.Message = "Server error. " + ex.Message;
            }
            return Ok(result);
        }

        [Authorize(Roles = "DeleteUser")]
        [HttpDelete]
        [Route("DeleteUserById")]
        public async Task<IActionResult> DeleteUser(int UserId)
        {
            IGeneralResult<string> result = new GeneralResult<string>();
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
                if (UserId > 0)
                {
                    TblUser user = new TblUser();
                    if (LoggedInUserRole != null && LoggedInUserRole.RoleId == (int)AppRole.Admin)
                    {
                        user = _userRepo.GetAllQuerable().Where(x => x.Id == UserId && x.IsDelete == false).FirstOrDefault();
                    }
                    else
                    {
                        user = _userRepo.GetAllQuerable().Where(x => x.Id == UserId && x.IsDelete == false && x.ClientId == Convert.ToInt32(LoggedInUserClientId)).FirstOrDefault();
                    }
                    if (user == null)
                    {
                        result.Message = " User does Not Exist";
                        return Ok(result);
                    }
                    else
                    {
                        user.IsActive = false;
                        user.IsDelete = true;
                        user.ModifiedDate = DateTime.Now;
                        await _userRepo.Update(user);

                        result.IsSuccess = true;
                        result.Message = "User deleted successfully.";
                    }

                }
                else
                {
                    result.Message = "Please enter a valid User UserId.";
                }
            }
            catch (Exception ex)
            {
                result.Message = "Server error! " + ex.Message;
            }
            return Ok(result);
        }

        [Authorize(Roles = "EditUser")]
        [HttpGet]
        [Route("UserActiveInactive")]
        public async Task<IActionResult> ActiveAndInactive(int UserId)
        {
            IGeneralResult<UserViewVm> result = new GeneralResult<UserViewVm>();
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
                if (UserId > 0)
                {
                    TblUser user = new TblUser();
                    if (LoggedInUserRole != null && LoggedInUserRole.RoleId == (int)AppRole.Admin)
                    {
                        user = _userRepo.GetAllQuerable().Where(x => x.Id == UserId && x.IsDelete == false).FirstOrDefault();
                    }
                    else
                    {
                        user = _userRepo.GetAllQuerable().Where(x => x.Id == UserId && x.IsDelete == false && x.ClientId == Convert.ToInt32(LoggedInUserClientId)).FirstOrDefault();
                    }
                    if (user == null)
                    {
                        result.Message = " User does Not Exist";
                        return Ok(result);
                    }
                    else
                    {
                        user.IsActive = !user.IsActive;
                        user.ModifiedDate = DateTime.Now;
                        await _userRepo.Update(user);

                        result.IsSuccess = true;
                        result.Message = "User updated successfully.";
                        result.Data = (UserViewVm)user;
                        return Ok(result);

                    }
                }
                else
                {
                    result.Message = "Please enter a valid User UserId.";
                }
            }
            catch (Exception ex)
            {
                result.Message = "Server error! " + ex.Message;
            }
            return Ok(result);
        }

        [Authorize(Roles = "ViewUser")]
        [HttpGet]
        [Route("GetUserById")]
        public async Task<IActionResult> GetUserDetailsById(int UserId)
        {
            IGeneralResult<UserViewVm> result = new GeneralResult<UserViewVm>();
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
                if (UserId > 0)
                {
                    TblUser user = new TblUser();
                    if (LoggedInUserRole != null && LoggedInUserRole.RoleId == (int)AppRole.Admin)
                    {
                        user = _userRepo.GetAllQuerable().Where(x => x.Id == UserId && x.IsDelete == false).FirstOrDefault();
                    }
                    else
                    {
                        user = _userRepo.GetAllQuerable().Where(x => x.Id == UserId && x.IsDelete == false && x.ClientId == Convert.ToInt32(LoggedInUserClientId)).FirstOrDefault();
                    }
                    if (user == null)
                    {
                        result.Message = " User does Not Exist";
                        return Ok(result);
                    }
                    result.IsSuccess = true;
                    result.Message = "User fetched successfully.";
                    result.Data = (UserViewVm)user;
                }
                else
                {
                    result.Message = "Please enter a valid User UserId.";
                }
            }
            catch (Exception ex)
            {
                result.Message = "Server error! " + ex.Message;
            }
            return Ok(result);
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

        private async Task<IGeneralResult<string>> AddorUpdateRole(int userId, AppRole appRole, int roleId)
        {
            IGeneralResult<string> result = new GeneralResult<string>();

            var checkUserAppRole = _userAppRoleRepo.GetAllQuerable().Where(x => x.UserId == userId).FirstOrDefault();

            if (checkUserAppRole == null)
            {
                
                TblUserAppRole userAppRole = new TblUserAppRole()
                {
                    UserId = userId,
                    RoleId = (int)appRole
                };
                var roleAdd = await _userAppRoleRepo.Add(userAppRole);
                if (roleAdd.Id == 0)
                {
                    result.Message = "Something Went Wrong";
                    return result;
                }
            }
            var checkUserRole = _UserRoleRepo.GetAllQuerable().Where(x => x.UserId == userId).FirstOrDefault();

            if (checkUserRole != null)
            {
                await _UserRoleRepo.Update(checkUserRole);
            }
            else
            {
                TblUserRole userRole = new TblUserRole
                {
                    RoleId = roleId,
                    UserId = userId
                };
                var userRoleData = await _UserRoleRepo.Add(userRole);
                if (userRoleData.Id == 0)
                {
                    result.Message = "Something Went Wrong";
                    return result;
                }
            }

            result.IsSuccess = true;
            result.Message = "Role Added Successfully";
            return result;
        }
    }
}
