using Microsoft.Data.Entity;
using Microsoft.AspNet.Identity.EntityFramework;

namespace InternEvaluate.Models
{
    public class EvaluateContext : IdentityDbContext<User>
    {
        public DbSet<Comment> Comments { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Comment>(e => 
            {
                // 为评定等级添加索引
                e.Index(x => x.Level);

                // 为组长评价标记添加索引
                e.Index(x => x.IsChargeman);
            });

            builder.Entity<User>(e => 
            {
                // 为班级、学号和组别添加索引
                e.Index(x => x.Class);
                e.Index(x => x.Group);
                e.Index(x => x.StudentNumber);

                // 在这里因为User中包含两个ICollection<Comment>，并且Comment中包含两个UserId，因此系统无法自动识别，需要在此处声明对应关系
                e.Collection(x => x.Evaluate)
                    .InverseReference(x => x.Evaluator);
                e.Collection(x => x.Evaluated)
                    .InverseReference(x => x.Evaluated);
            });
        }
    }
}
