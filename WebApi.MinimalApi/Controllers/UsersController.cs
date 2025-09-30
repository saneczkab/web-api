using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using WebApi.MinimalApi.Domain;
using WebApi.MinimalApi.Models;

namespace WebApi.MinimalApi.Controllers;

[Produces("application/json", "application/xml")]
[Route("api/[controller]")]
[ApiController]
public class UsersController : Controller
{
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;
    private readonly LinkGenerator _linkGenerator;
    
    // Чтобы ASP.NET положил что-то в userRepository требуется конфигурация
    public UsersController(IUserRepository userRepository, IMapper mapper, LinkGenerator linkGenerator)
    {
        _userRepository = userRepository;
        _mapper = mapper;
        _linkGenerator = linkGenerator;
    }

    [HttpGet("{userId}", Name = nameof(GetUserById))]
    [HttpHead("{userId}", Name = nameof(GetUserById))]
    public ActionResult<UserDto> GetUserById([FromRoute] Guid userId)
    {
        var user = _userRepository.FindById(userId);
        if (user == null)
            return NotFound();
        
        var userDto = _mapper.Map<UserDto>(user);
        
        if (!HttpMethods.IsHead(Request.Method))
            return userDto;
            
        Response.Headers.ContentType = "application/json; charset=utf-8";
        return Ok();
    }

    [HttpGet(Name = nameof(GetUsers))]
    public IActionResult GetUsers([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 20);
        
        var pageList = _userRepository.GetPage(pageNumber, pageSize);
        var users = _mapper.Map<IEnumerable<UserDto>>(pageList);
        var paginationHeader = new
        {
            previousPageLink = pageList.HasPrevious
                ? _linkGenerator.GetUriByRouteValues(HttpContext, nameof(GetUsers), values: new
                {
                    pageNumber = pageNumber - 1, 
                    pageSize
                })
                : null,
            nextPageLink = pageList.HasNext
                ? _linkGenerator.GetUriByRouteValues(HttpContext, nameof(GetUsers), values: new
                {
                    pageNumber = pageNumber + 1, 
                    pageSize
                })
                : null,
            totalCount = pageList.TotalCount,
            pageSize = pageSize,
            currentPage = pageNumber,
            totalPages = (int)Math.Ceiling((double)pageList.TotalCount / pageSize),
        };
        
        Response.Headers.Append("X-Pagination", JsonConvert.SerializeObject(paginationHeader));
        
        return Ok(users);
    }

    [HttpPost]
    public IActionResult CreateUser([FromBody] CreateUserDto? user)
    {
        if (user is null)
            return BadRequest();
        
        if (string.IsNullOrEmpty(user.Login) || !user.Login.All(char.IsLetterOrDigit))
        {
            ModelState.AddModelError("Login", "Invalid login format");
            return UnprocessableEntity(ModelState);
        }
        
        var entity = _mapper.Map<UserEntity>(user);
        var createdId = _userRepository.Insert(entity).Id;
        
        return CreatedAtRoute(nameof(GetUserById), new { userId = entity.Id }, createdId);
    }

    [HttpPut("{userId}")]
    public IActionResult UpdateUser([FromRoute] string userId, [FromBody] UpdateUserDto? user)
    {
        if (!Guid.TryParse(userId, out var userGuid) || user == null)
            return BadRequest();

        if (!ModelState.IsValid)
            return UnprocessableEntity(ModelState);


        var entity = new UserEntity(userGuid);
        _mapper.Map(user, entity);
        _userRepository.UpdateOrInsert(entity, out var inserted);
        
        if (inserted)
            return CreatedAtRoute(nameof(GetUserById), new { userId = userGuid }, userGuid);
        
        return NoContent();
    }
    
    [HttpDelete("{userId:guid}")]
    public IActionResult DeleteUser([FromRoute] Guid userId)
    {
        if (_userRepository.FindById(userId) is null)
            return NotFound();
        _userRepository.Delete(userId);
        return NoContent();
    }

    [HttpOptions]
    public IActionResult Options()
    {
        Response.Headers.Append("Allow", "GET, POST, OPTIONS");
        return Ok();
    }
}