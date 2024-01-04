﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using API.Data;
using API.Entities;
using Microsoft.AspNetCore.Authorization;
using API.Interfaces;
using API.Helpers;
using API.DTOs;
using AutoMapper;
using System.Security.Claims;
using API.Extensions;
using API.Services;
using CloudinaryDotNet.Actions;

namespace API.Controllers;

[Authorize]
public class UsersController : BaseApiController
{
    private readonly IMapper _mapper;
    private readonly IPhotoService _photoService;
    private readonly IUnitOfWork _uow;

    public UsersController(IMapper mapper, IPhotoService photoService, IUnitOfWork uow)
    {
        _mapper = mapper;
        _photoService = photoService;
        _uow = uow;
    }

    [HttpGet]
    public async Task<ActionResult<PagedList<MemberDto>>> GetUsers(
        [FromQuery] UserParams userParams
    )
    {
        var gender = await _uow.UserRepository.GetUserGender(User.GetUsername());
        userParams.CurrentUsername = User.GetUsername();

        if (string.IsNullOrEmpty(userParams.Gender))
            userParams.Gender = gender == "male" ? "female" : "male";

        var users = await _uow.UserRepository.GetMembersAsync(userParams);

        Response.AddPaginationHeader(
            new PaginationHeader(
                users.CurrentPage,
                users.PageSize,
                users.TotalCount,
                users.TotalPages
            )
        );

        return Ok(users);
    }

    [HttpGet("{username}")]
    public async Task<ActionResult<MemberDto>> GetUser(string username)
    {
        var currentUsername = User.GetUsername();
        return await _uow.UserRepository.GetMemberAsync(
            username,
            isCurrentUser: currentUsername == username
        );
    }

    [HttpPut]
    public async Task<ActionResult> UpdateUser(MemberUpdateDto memberUpdateDto)
    {
        var user = await _uow.UserRepository.GetUserByUsernameAsync(User.GetUsername());

        if (user == null)
            return NotFound();

        _mapper.Map(memberUpdateDto, user);

        if (await _uow.Complete())
            return NoContent();
        return BadRequest("Failed to update user.");
    }

    [HttpPost("add-photo")]
    public async Task<ActionResult<PhotoDto>> AddPhoto(IFormFile file)
    {
        var user = await _uow.UserRepository.GetUserByUsernameAsync(User.GetUsername());
        if (user == null)
            return NotFound();

        var result = await _photoService.AddPhotoAsync(file);

        if (result.Error != null)
            return BadRequest(result.Error.Message);

        var photo = new Photo() { Url = result.SecureUrl.AbsoluteUri, PublicId = result.PublicId, };

        user.Photos.Add(photo);

        if (await _uow.Complete())
        {
            return CreatedAtAction(
                nameof(GetUser),
                new { username = user.UserName },
                _mapper.Map<PhotoDto>(photo)
            );
        }
        return BadRequest("Problem adding Photo");
    }

    [HttpPut("set-main-photo/{photoId}")]
    public async Task<ActionResult> SetMainPhoto(int photoId)
    {
        var user = await _uow.UserRepository.GetUserByUsernameAsync(User.GetUsername());

        if (user == null)
            return NotFound();

        var photo = user.Photos.FirstOrDefault(x => x.Id == photoId);

        if (photo == null)
            return NotFound();

        if (photo.IsMain)
            return BadRequest("This is already your main photo");

        if (!photo.IsApproved)
            return BadRequest("You cannot set unapproved photo as main");

        var currentMain = user.Photos.FirstOrDefault(x => x.IsMain);
        if (currentMain != null)
            currentMain.IsMain = false;

        photo.IsMain = true;
        if (await _uow.Complete())
            return NoContent();
        return BadRequest("Problem setting the main photo");
    }

    [HttpDelete("delete-photo/{photoId}")]
    public async Task<ActionResult> DeletePhoto(int photoId)
    {
        var user = await _uow.UserRepository.GetUserByUsernameAsync(User.GetUsername());

        var photo = await _uow.PhotoRepository.GetPhotoById(photoId);

        if (!user.Photos.Any(x => x.Id == photo.Id))
            return BadRequest();

        if (photo == null)
            return NotFound();

        if (photo.IsMain)
            return BadRequest("You cannot delete your main photo");

        if (photo.PublicId != null)
        {
            var result = await _photoService.DeletePhotoAsync(photo.PublicId);
            if (result.Error != null)
                return BadRequest(result.Error.Message);
        }

        user.Photos.Remove(photo);

        if (await _uow.Complete())
            return Ok();

        return BadRequest("Problem deleting photo");
    }
}
