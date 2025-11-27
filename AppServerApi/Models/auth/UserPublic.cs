public class UserPublic
{
    public int Id { get; }
    public string Username { get; }
    public Role Role { get; }

    public UserPublic(int id, string username, Role role)
    {
        Id = id;
        Username = username;
        Role = role;
    }

    public UserPublic(string username, Role role)
        : this(0, username, role)
    {
    }
}