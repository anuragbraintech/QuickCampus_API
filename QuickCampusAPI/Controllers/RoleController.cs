﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuickCampus_Core.Common;
using QuickCampus_Core.Interfaces;
using QuickCampus_Core.ViewModel;

namespace QuickCampusAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class RoleController : Controller
    {
        private readonly IRoleRepo roleRepo;
        private readonly IUserRepo userRepo;
        private readonly IClientRepo clientRepo;
        private IConfiguration config;
        public RoleController(IRoleRepo roleRepo, IUserRepo userRepo, IClientRepo clientRepo, IConfiguration config)
        {
            this.roleRepo = roleRepo;
            this.userRepo = userRepo;
            this.clientRepo = clientRepo;
            this.config = config;
        }

        [Authorize(Roles = "AddRole")]
        [HttpPost]
        [Route("roleAdd")]
        public async Task<IActionResult> roleAdd([FromBody] RoleModel vm)
        {
            IGeneralResult<RoleVm> result = new GeneralResult<RoleVm>();
            var _jwtSecretKey = config["Jwt:Key"];
            if (roleRepo.Any(x => x.Name == vm.RoleName))
            {
                result.Message = "RoleName Already Registerd!";
            }
            else
            {

                if (ModelState.IsValid)
                {
                    var userId = JwtHelper.GetuIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);
                    if(string.IsNullOrEmpty(userId))
                    {
                        result.Message = "User Not Found";
                        result.IsSuccess = false;
                        return Ok(result);
                    }
                    var user = await userRepo.GetById( Convert.ToInt32(userId));
                    var res = (user != null && user.IsDelete == true) ? user : null;
                    if (user != null)
                    {
                        var clientId = JwtHelper.GetUserIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);

                        if (!string.IsNullOrEmpty(clientId))
                        {
                            int parsedClientId;
                            if (int.TryParse(clientId, out parsedClientId))
                            {
                                RoleVm roleVm = new RoleVm
                                {
                                    Name = vm.RoleName,
                                    ClientId = parsedClientId,
                                    CreatedBy = user.Id,
                                    ModifiedBy = user.Id,
                                    CreatedDate = DateTime.Now,
                                    ModofiedDate = DateTime.Now
                                };
                                await roleRepo.Add(roleVm.ToRoleDBModel());
                                result.Message = "Role added successfully";
                                result.IsSuccess = true;
                                result.Data = roleVm;
                                return Ok(result);
                            }
                            else
                            {
                                result.Message = "Invalid Client ID format.";
                            }

                        }
                        else
                        {
                            RoleVm roleVm = new RoleVm
                            {
                                Name = vm.RoleName,
                                ClientId = null,
                                CreatedBy = user.Id,
                                ModifiedBy = user.Id,
                                CreatedDate = DateTime.Now,
                                ModofiedDate = DateTime.Now
                            };
                            await roleRepo.Add(roleVm.ToRoleDBModel());
                            result.Message = "Role added successfully";
                            result.IsSuccess = true;
                            result.Data = roleVm;
                            return Ok(result);
                        }
                    }
                    else
                    {
                        result.Message = "User Id is not valid.";
                    }
                }
                else
                {
                    result.Message = GetErrorListFromModelState.GetErrorList(ModelState);
                }
                return Ok(result);
            }
            return Ok(result);
        }

        [Authorize(Roles = "GetAllRole")]
        [HttpGet]
        [Route("roleList")]
        public async Task<IActionResult> roleList()
        {
            var _jwtSecretKey = config["Jwt:Key"];
            var  clientId = JwtHelper.GetClientIdFromToken(Request.Headers["Authorization"], _jwtSecretKey); 
            List<RoleResponseList> roleVm = new List<RoleResponseList>();
            var rolelist = (await roleRepo.GetAll()).ToList();

            if (string.IsNullOrEmpty(clientId))
            {
                roleVm = rolelist.Select(x => (RoleResponseList)x).Where(w=>w.ClientId==null).ToList();
            }
            else
            {
                roleVm = rolelist.Select(x => (RoleResponseList)x).Where(w => w.ClientId == Convert.ToInt32(clientId)).ToList();
            }
            return Ok(roleVm);
        }

        [Authorize(Roles = "UpdateRole")]
        [HttpPost]
        [Route("roleEdit")]
        public async Task<IActionResult> Edit(int roleId, RoleModel vm)
        {
            IGeneralResult<RoleVm> result = new GeneralResult<RoleVm>();
            var _jwtSecretKey = config["Jwt:Key"];
            if (roleRepo.Any(x => x.Name == vm.RoleName && x.Id != roleId))
            {
                result.Message = "RoleName Already Registerd!";
            }
            else
            {
                var uId = await userRepo.GetById(vm.userId);
                var check = (uId != null && uId.IsDelete == true) ? uId : null;
                if (uId != null)
                {
                    var res = await roleRepo.GetById(roleId);
                    var clientId = JwtHelper.GetClientIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);

                    if (clientId != null || clientId == "")
                    {
                        res.Id = roleId;
                        if (clientId == "")
                        {
                            res.ClientId = null; // Assign null to ClientId property
                        }
                        else
                        {
                            res.ClientId = Convert.ToInt32(clientId);
                        }
                        if (res != null)
                        {
                            res.Name = vm.RoleName;
                            res.ModifiedBy = vm.userId;
                            res.ModofiedDate = DateTime.Now;
                            await roleRepo.Update(res);
                            result.Message = "Role data is updated successfully";
                            result.IsSuccess = true;
                            result.Data = (RoleVm)res;
                            return Ok(result);
                        }
                        else
                        {
                            result.Message = GetErrorListFromModelState.GetErrorList(ModelState);
                        }
                    }
                    else
                    {
                        result.Message = "ClientId not found. ";
                    }
                }
                else
                {
                    result.Message = "User Id is not valid.";
                }
                return Ok(result);

            }
            return Ok(result);
        }

        [Authorize(Roles = "EditRole")]
        [HttpGet]
        [Route("GetRoleByRoleId")]
        public async Task<IActionResult> GetRoleByRoleId(int roleId)
        {
            int cId = 0;
            var _jwtSecretKey = config["Jwt:Key"];
            var clientId = JwtHelper.GetClientIdFromToken(Request.Headers["Authorization"], _jwtSecretKey);
            IGeneralResult<GetRoleId> roleRecord = new GeneralResult<GetRoleId>();

            if (string.IsNullOrEmpty(clientId))
            {
                 roleRecord.Data = (await roleRepo.GetAll()).Where(w => w.Id == roleId && w.ClientId==null).Select(s => new GetRoleId()
                {
                    Id = roleId,
                    RoleName = s.Name
                }).FirstOrDefault();
            }
            else
            {
                cId= Convert.ToInt32(clientId);
                 roleRecord.Data = (await roleRepo.GetAll()).Where(w => w.Id == roleId && w.ClientId==cId).Select(s => new GetRoleId()
                {
                    Id = roleId,
                    RoleName = s.Name
                }).FirstOrDefault();
            }

            if (roleRecord.Data == null)
            {
                roleRecord.IsSuccess = false;
                roleRecord.Message = "No Record Found";
            }
            else
            {
                roleRecord.IsSuccess = true;
                roleRecord.Message = "Role Record of Id "+roleId;
            }
            return Ok(roleRecord);
        }


        [HttpPost]
        [Route("SetRolePermissions")]
        public async Task<IActionResult> SetRolePermissions(RoleMappingRequest roleMappingRequest)
        {
            var response = await roleRepo.SetRolePermission(roleMappingRequest);
            return Ok(response);
        }
    }
}
