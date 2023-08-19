
namespace ApothedocImportLib.DataItem
{
    public class User
    {
        public int id { get; set; }
        public string firstName { get; set;}
        public string lastName { get; set; }

    }

    public class UserListWrapper
    {
        public List<User>? User { get; set; }
    }
}