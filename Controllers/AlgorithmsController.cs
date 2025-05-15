using Microsoft.AspNetCore.Mvc;
using Optimizator.Services;
using System;
using System.ComponentModel.DataAnnotations;

namespace Optimizator.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AlgorithmsController : ControllerBase
    {
        private readonly IAlgorithmService _algorithmService;

        public AlgorithmsController(IAlgorithmService algorithmService)
        {
            _algorithmService = algorithmService;
        }

        [HttpGet]
        public IActionResult GetAllAlgorithms()
        {
            try
            {
                var algorithms = _algorithmService.GetAllAlgorithms();
                return Ok(algorithms);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpGet("{name}")]
        public IActionResult GetAlgorithm(string name)
        {
            try
            {
                var algorithm = _algorithmService.GetAlgorithmDefinition(name);
                return Ok(algorithm);
            }
            catch (Exception ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        //[HttpPost("{name}")]
        //public IActionResult ExecuteAlgorithm(string name, [FromBody] ExecutionRequest request)
        //{
        //    try
        //    {
        //        var parameters = request.Parameters.ToDictionary(
        //            p => p.Name,
        //            p => (object)p.Value);

        //        var result = _algorithmService.ExecuteAlgorithm(name, parameters);
        //        return Ok(result);
        //    }
        //    catch (Exception ex)
        //    {
        //        return BadRequest(new { message = ex.Message });
        //    }
        //}
        [HttpPost("{name}")]
        public IActionResult ExecuteAlgorithm(string name, [FromBody] ExecutionRequest request)
        {
            try
            {
                var parameters = request.Parameters.ToDictionary(
                    p => p.Name,
                    p => (object)p.Value);

                var result = _algorithmService.ExecuteAlgorithm(name, parameters);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }

    public class ExecutionRequest
    {
        [Required]
        public List<ParameterValue> Parameters { get; set; } = new();
    }

    public class ParameterValue
    {
        [Required]
        public string Name { get; set; }

        [Required]
        public string Value { get; set; }
    }
}