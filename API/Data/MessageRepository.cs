using System.Runtime.CompilerServices;
using API.DTOs;
using API.Entities;
using API.Helpers;
using API.Interfaces;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data
{
    public class MessageRepository : IMessageRepository
    {
        private readonly DataContext _context;
        private readonly IMapper _mapper;

        public MessageRepository(DataContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public void AddMessage(Message message)
        {
            _context.Messages.Add(message);
        }

        public void DeleteMessage(Message message)
        {
            _context.Messages.Remove(message);
        }

        public async Task<Message> GetMessage(int id)
        {
            return await _context.Messages.FindAsync(id);
        }

        public async Task<PagedList<MessageDto>> GetMessagesForUser(MessageParams messageParams)
        {
            var query = _context.Messages.OrderByDescending(x => x.MessageSent).AsQueryable();

            query = messageParams.Container switch
            {
                "Inbox" => query.Where(u => u.RecipientUsername == messageParams.Username && u.RecipientDeleted == false),
                "Outbox" => query.Where(u => u.SenderUsername == messageParams.Username && u.SenderDeleted == false),
                _
                    => query.Where(
                        u => u.RecipientUsername == messageParams.Username && u.DateRead == null && u.RecipientDeleted == false
                    )
            };

            var messages = query.ProjectTo<MessageDto>(_mapper.ConfigurationProvider);  // In the process of building a query

            return await PagedList<MessageDto>.CreateAsync(
                messages,
                messageParams.PageNumber,
                messageParams.PageSize
            );
        }

        public async Task<IEnumerable<MessageDto>> GetMessageThread(string currentUserName, string recipientUserName)
        {
            var messages = await _context.Messages
                .Include(m => m.Sender)
                .ThenInclude(p => p.Photos)
                .Include(m => m.Recipient)
                .ThenInclude(p => p.Photos)
                .Where(
                    m =>
                        m.SenderUsername == currentUserName
                            && m.RecipientUsername == recipientUserName && m.SenderDeleted == false
                        || m.SenderUsername == recipientUserName
                            && m.RecipientUsername == currentUserName && m.RecipientDeleted == false
                )
                .OrderBy(m => m.MessageSent)
                .ToListAsync();

            var unreadMessages = messages
                .Where(m => m.DateRead == null && m.RecipientUsername == currentUserName)
                .ToList();
            
            if (unreadMessages.Any())
            {
                foreach (var message in unreadMessages)
                {
                    message.DateRead = DateTime.UtcNow;
                }
                await _context.SaveChangesAsync();
            }

            return _mapper.Map<IEnumerable<MessageDto>>(messages);  // Existing C# objects to DTO
        }

        public async Task<bool> SaveAllAsync()
        {
            return await _context.SaveChangesAsync() > 0;
        }
    }
}