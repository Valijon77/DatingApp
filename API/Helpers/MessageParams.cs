namespace API.Helpers
{
    public class MessageParams : PaginationParams
    {
        public string Username { get; set; }  // current username
        public string Container { get; set; } = "Unread";
    }
}
