﻿using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using QuickCampus_Core.Common;
using QuickCampus_Core.Interfaces;
using QuickCampus_Core.Services;
using QuickCampus_Core.ViewModel;

namespace QuickCampusAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RoleController : Controller
    {
        private readonly IRoleRepo roleRepo;
        private readonly IUserRepo userRepo;
        private readonly IClientRepo clientRepo;
        public RoleController(IRoleRepo roleRepo, IUserRepo userRepo, IClientRepo clientRepo)
        {
            this.roleRepo = roleRepo;
            this.userRepo = userRepo;
            this.clientRepo = clientRepo;
        }
        [HttpPost]
        [Route("roleAdd")]
        public async Task<IActionResult> roleAdd([FromBody] RoleModel vm)
        {
            IGeneralResult<RoleVm> result = new GeneralResult<RoleVm>();
            if (roleRepo.Any(x => x.Name == vm.RoleName))
            {
                result.Message = "RoleName Already Registerd!";
            }
            else
            {

                if (ModelState.IsValid)
                {
                    var user = await userRepo.GetById(vm.userId);
                    var res = (user != null && user.IsDelete == true) ? user : null;
                    if (user != null)
                    {
                        var clientId = vm.ClientId.HasValue ? await clientRepo.GetById((int)vm.ClientId) : null;

                        if (clientId != null || vm.ClientId == null)
                        {
                            RoleVm roleVm = new RoleVm
                        {
                            Name = vm.RoleName,
                            ClientId = (int)vm.ClientId,
                            CreatedBy = user.Id,
                            ModifiedBy = user.Id,
                            CreatedDate = DateTime.Now,

                        };
                        await roleRepo.Add(roleVm.toRoleDBModel());
                        result.Message = "Role added successfully";
                        result.IsSuccess = true;
                        result.Data = roleVm;
                        return Ok(result);
                        }
                        else
                        {
                            result.Message = "Client Id is not valid.";
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

        [HttpGet]
        [Route("roleList")]
        public async Task<IActionResult> roleList()
        {
            List<RoleVm> roleVm = new List<RoleVm>();
            var rolelist = (await roleRepo.GetAll()).ToList();
            roleVm = rolelist.Select(x => ((RoleVm)x)).ToList();
            return Ok(roleVm);
        }

        [HttpPost]
        [Route("roleEdit")]
        public async Task<IActionResult> Edit(int roleId, RoleModel vm)
        {
            IGeneralResult<RoleVm> result = new GeneralResult<RoleVm>();
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
                    var clientId = vm.ClientId.HasValue ? await clientRepo.GetById((int)vm.ClientId) : null;

                    if (clientId != null || vm.ClientId == null)
                    {
                        if (res != null)
                        {
                            res.Id = roleId;
                            res.ClientId = vm.ClientId;
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
    }
}