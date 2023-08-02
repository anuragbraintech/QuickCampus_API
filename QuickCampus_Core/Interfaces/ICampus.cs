﻿using QuickCampus_Core.Common;
using QuickCampus_Core.ViewModel;
using QuickCampus_DAL.Context;

namespace QuickCampus_Core.Interfaces
{
    public interface ICampusRepo : IGenericRepository<WalkIn>
    {
        Task<IEnumerable<CampusGridViewModel>> GetAllCampus(int clientId);
        Task<IGeneralResult<CampusGridViewModel>> GetCampusByID(int id, int clientId);
        Task<IEnumerable<CampusGridViewModel>> Add(CampusGridViewModel campusGridViewModel);
        Task<IGeneralResult<string>> AddCampus(CampusGridRequestVM vm, int clientId, int userId);

        Task<IGeneralResult<CampusGridViewModel>> UpdateCampusStatus(int id, int clientId, bool status);
        Task<IGeneralResult<CampusGridViewModel>> DeleteCampus(int id, int clientId, bool isDelete);
    }
}
