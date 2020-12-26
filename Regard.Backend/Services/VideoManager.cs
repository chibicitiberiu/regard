using Regard.Backend.DB;
using Regard.Backend.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Services
{
    public class VideoManager
    {
        private readonly DataContext dataContext;

        public VideoManager(DataContext dataContext)
        {
            this.dataContext = dataContext;
        }

        public IQueryable<Video> GetAll(UserAccount userAccount)
        {
            return dataContext.Videos.AsQueryable()
                .Where(x => x.Subscription.UserId == userAccount.Id);
        }
    }
}
