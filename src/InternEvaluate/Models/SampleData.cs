using System;
using System.Threading.Tasks;
using Microsoft.Framework.DependencyInjection;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;

namespace InternEvaluate.Models
{
    public static class SampleData
    {
        public async static Task InitDB(IServiceProvider service)
        {
            // 获取数据库上下文服务
            var DB = service.GetRequiredService<EvaluateContext>();

            // 获取UserManager服务
            var userManager = service.GetRequiredService<UserManager<User>>();

            // 获取RoleManager服务
            var roleManager = service.GetRequiredService<RoleManager<IdentityRole>>();

            if (DB.Database != null && DB.Database.EnsureCreated())
            {
                // 初始化角色
                await roleManager.CreateAsync(new IdentityRole { Name = "系主任" });
                await roleManager.CreateAsync(new IdentityRole { Name = "组长" });
                await roleManager.CreateAsync(new IdentityRole { Name = "学生" });

                // 初始化系主任，并为其添加系主任角色
                var user = new User { Name = "张某某", UserName = "Admin" };
                await userManager.CreateAsync(user, "Yuuko19931101!@#");
                await userManager.AddToRoleAsync(user, "系主任");
            }
        }
    }
}
