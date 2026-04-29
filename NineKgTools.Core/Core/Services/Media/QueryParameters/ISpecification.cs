namespace NineKgTools.Core.Services.Media.QueryParameters;

public interface ISpecification<T>
{
    IQueryable<T> Apply(IQueryable<T> query);
}
