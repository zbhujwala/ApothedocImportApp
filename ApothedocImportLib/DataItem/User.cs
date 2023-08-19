
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
        public List<User>? Users { get; set; }
    }

    public class UserIdMapping
    { 
        public int SourceId { get; set; }
        public int TargetId { get; set; }
    }

    public class UserIdMappingWrapper
    {
        public List<UserIdMapping>? Mappings { get; set; }
    }

}