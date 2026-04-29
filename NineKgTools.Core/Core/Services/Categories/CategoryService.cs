using NineKgTools.Core.DbContexts;
using NineKgTools.Core.Models.Categories;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace NineKgTools.Core.Services.Categories;

public class CategoryService(MediaDbContext dbContext)
{

    /// <summary>
    /// 获取所有分类
    /// </summary>
    public List<Category> GetAllCategories()
    {
        return StaticCategories.CategoryList;
    }

    /// <summary>
    /// 寻找Category
    /// </summary>
    public async Task<Category> FindCategoryAsync(Category category)
    {
        return await dbContext.Categories.FindAsync(category.Id) ?? StaticCategories.Unknown;
    }

    public async Task InitializeCategoriesDb()
    {
        Log.Information("正在初始化分类...");

        foreach (var category in StaticCategories.CategoryList)
        {
            var dbCategory = await dbContext.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == category.Id);
            if (dbCategory == null)
            {
                await dbContext.Categories.AddAsync(category);
                await dbContext.SaveChangesAsync();
            }
        }

        Log.Information("分类初始化完毕");
    }
}