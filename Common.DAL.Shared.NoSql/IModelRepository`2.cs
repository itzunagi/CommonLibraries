using Common.BusinessLogic.Models;
using Common.Results.Models;

namespace Common.DAL.Shared.NoSql
{
    public interface IModelRepository<TBusinessModel, in TId>
        where TBusinessModel : ModelBase<TId>
    {
        Task<IResult<TBusinessModel>> CreateAsync(TBusinessModel model);
        Task<IResult<TBusinessModel>> GetAsync(TId id);
        Task<IResult> DeleteAsync(TId id);
        Task<IResult> SaveAsync(TId id, TBusinessModel model);
    }
}
