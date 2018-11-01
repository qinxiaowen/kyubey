using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Andoromeda.Kyubey.Models;
using Andoromeda.Kyubey.Portal.Models;
using Pomelo.AspNetCore.Localization;
using System.IO;
using Newtonsoft.Json;

namespace Andoromeda.Kyubey.Portal.Controllers
{
    public class HomeController : BaseController
    {
        public HomeController(ICultureProvider cultureProvider) : base(cultureProvider)
        {

        }
        [Route("/")]
        public async Task<IActionResult> Index([FromServices] KyubeyContext db)
        {
            var currentLanguage = _cultureProvider.DetermineCulture();

            //linq 待优化
            var tokens = await db.TokenHatchers
                .Include(x => x.Token)
                .Select(x => new TokenHandlerListVM()
                {
                    BannerId = db.TokenBanners.Where(b => b.TokenId == x.TokenId).OrderBy(b => b.BannerOrder).FirstOrDefault().Id.ToString(),
                    Id = x.TokenId,
                    Introduction = x.Introduction,
                    TargetCredits = x.TargetCredits,
                    CurrentRaised = x.CurrentRaisedSum,
                    ShowGoExchange = true,
                })
                .ToListAsync();

            return View(tokens);
        }

        public async Task<IActionResult> GenerateJsonTokenFile([FromServices] KyubeyContext db)
        {
            var tokens = await db.Tokens.Include(x => x.User).ToListAsync();
            var rootFolder = System.AppDomain.CurrentDomain.BaseDirectory;
            var rootTokensFolder = System.IO.Path.Combine(rootFolder, @"Tokens");

            var dexs = db.Otcs.ToList();
            var bancors = db.Bancors.ToList();
            var hatchers = db.TokenHatchers.ToList();

            foreach (var t in tokens)
            {
                var tokenFolder = System.IO.Path.Combine(rootTokensFolder, t.Name);
                var filePath = System.IO.Path.Combine(tokenFolder, @"manifest.json");
                if (!System.IO.Directory.Exists(tokenFolder))
                {
                    Directory.CreateDirectory(tokenFolder);
                }
                if (!System.IO.File.Exists(filePath))
                {
                    // Create a file to write to.
                    using (StreamWriter sw = System.IO.File.CreateText(filePath))
                    {
                        var tokenJObj = new TokenManifestJObject()
                        {
                            Id = t.Id,
                            Owners = t.User.UserName,
                            Priority = 0,
                            Dex = dexs.Exists(x => x.Id == t.Id),
                            Incubation = hatchers.Exists(x => x.TokenId == t.Id),
                            Contract_Exchange = bancors.Exists(x => x.Id == t.Id),
                            Basic = new TokenManifestBasicJObject()
                            {
                                Contract = new TokenManifestBasicContractJObject()
                                {
                                    pricing = t.Contract,
                                    transfer = t.Contract
                                },
                                Email = t.Email,
                                Github = t.GitHub,
                                Protocol = t.CurveId,
                                Tg = "",
                                Website = t.WebUrl

                            }
                        };
                        sw.Write(JsonConvert.SerializeObject(tokenJObj, Formatting.Indented));
                    }
                }
            }
            return Ok();
        }

        [Route("/Dex")]
        public async Task<IActionResult> Dex([FromServices] KyubeyContext db, string token = null, CancellationToken cancellationToken = default)
        {
            if (token != null)
            {
                token = token.ToUpper();
            }
            ViewBag.SearchToken = token;
            IEnumerable<Bancor> bancors = db.Bancors.Include(x => x.Token);
            if (!string.IsNullOrEmpty(token))
            {
                bancors = bancors.Where(x => x.Id.Contains(token) || x.Token.Name.Contains(token));
            }
            bancors = bancors
                .Where(x => x.Status == Status.Active)
                .OrderByDescending(x => x.Token.Priority)
                .ToList();

            IEnumerable<Otc> otcs = db.Otcs.Include(x => x.Token);
            if (!string.IsNullOrEmpty(token))
            {
                otcs = otcs.Where(x => x.Id.Contains(token) || x.Token.Name.Contains(token));
            }
            otcs = otcs
                .Where(x => x.Status == Status.Active)
                .OrderByDescending(x => x.Token.Priority)
                .ToList();

            var ret = new List<TokenDisplay>();
            var tokens = await DB.Tokens.ToListAsync(cancellationToken);
            foreach (var x in tokens)
            {
                if (!bancors.Any(y => y.Id == x.Id) && !otcs.Any(y => y.Id == x.Id))
                {
                    continue;
                }

                var t = new TokenDisplay
                {
                    Id = x.Id,
                    Name = x.Name,
                    Protocol = x.CurveId ?? SR["Unknown"],
                    ExchangeInDex = otcs.Any(y => y.Id == x.Id),
                    ExchangeInContract = bancors.Any(y => y.Id == x.Id)
                };

                if (t.ExchangeInDex)
                {
                    t.Change = otcs.Single(y => y.Id == x.Id).Change;
                    t.Price = otcs.Single(y => y.Id == x.Id).Price;
                }
                else
                {
                    t.Change = bancors.Single(y => y.Id == x.Id).Change;
                    t.Price = bancors.Single(y => y.Id == x.Id).BuyPrice;
                }

                ret.Add(t);
            }

            return View(ret);
        }
    }
}
