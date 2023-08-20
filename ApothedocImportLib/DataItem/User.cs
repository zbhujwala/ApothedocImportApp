
namespace ApothedocImportLib.DataItem
{
    public class User
    {
        public int Id { get; set; }
        public string FirstName { get; set;}
        public string LastName { get; set; }
        public bool? Disabled { get; set; }
        public string? Email { get; set; }
        public bool? OrgAdmin { get; set; }
        public Dictionary<int, bool>? ClinicLevelAccess { get; set; }
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
        public List<UserIdMapping>? UserMappings { get; set; }
        public List<UserIdMapping>? ProviderMappings { get; set; }
    }

}