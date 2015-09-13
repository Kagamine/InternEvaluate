using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Dnx.Runtime;
using Microsoft.Data.Entity;
using InternEvaluate.Models;

namespace InternEvaluate
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            // 获取应用程序环境服务
            var appEnv = services.BuildServiceProvider().GetRequiredService<IApplicationEnvironment>();

            // 添加Entity Framework服务
            services.AddEntityFramework()
                .AddSqlite()
                .AddDbContext<EvaluateContext>(x => x.UseSqlite("Data source=" + appEnv.ApplicationBasePath + "/evaluate.db")); // SQLite的数据库连接字符串

            // 添加Identity服务
            services.AddIdentity<User, IdentityRole>()
                .AddEntityFrameworkStores<EvaluateContext>()
                .AddDefaultTokenProviders();

            // 添加MVC服务
            services.AddMvc();           
            
            // 添加CodeComb扩展的CurrentUser服务，在前台可以直接使用已经扩展的User帮助类
            services.AddCurrentUser<string, User>();
        }

        public async void Configure(IApplicationBuilder app)
        {
            // 使用StaticFiles就会为wwroot下的文件添加到路由规则中
            app.UseStaticFiles();

            // 使用Identity中间件可以使标记有[Authorize]之类的action在未登录时跳转到登录页面
            app.UseIdentity();

            // 使用MVC中间件，并配置默认路由规则
            app.UseMvc(x => x.MapRoute("default", "{controller=Home}/{action=Index}/{id?}"));

            // 初始化数据库并添加初始数据
            await SampleData.InitDB(app.ApplicationServices);
        }
    }
}
