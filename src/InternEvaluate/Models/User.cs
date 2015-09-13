using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNet.Identity.EntityFramework;

namespace InternEvaluate.Models
{
    public class User : IdentityUser
    {
        // 班级
        [MaxLength(16)]
        public string Class { get; set; }

        // 姓名
        [MaxLength(8)]
        public string Name { get; set; }

        // 组
        [MaxLength(8)]
        public string Group { get; set; }

        // 学号
        [MaxLength(10)]
        public string StudentNumber { get; set; }

        // 获得的评价集合
        public virtual ICollection<Comment> Evaluated { get; set; } = new List<Comment>();

        // 给他人的评价集合
        public virtual ICollection<Comment> Evaluate { get; set; } = new List<Comment>();
    }
}
