using System.Threading.Tasks;
using Plugin.Messaging;

namespace Camera2Basic
{
    public class Settings
    {
        public string MessageRecipient { get; set; }
        public string MessageSubject { get; set; }
    }
    
    public class MailMessageProvider
    {
        private readonly Settings _settings;
        private readonly MailMessageProvider _mailMessageProvider;
        
        public string Provide(params string[] readings)
        {
            return "";
        }
        
        public void SendMessageAsync()
        {
            var readings = new string[0];
            
            var emailMessenger = CrossMessaging.Current.EmailMessenger;
            var message = _mailMessageProvider.Provide(readings);
            var email = new EmailMessageBuilder()
                    .To(_settings.MessageRecipient)
                    .Subject(_settings.MessageSubject)
                    .Body(message)
                    .Build();
            emailMessenger.SendEmail(email);
            
        }
    }
}