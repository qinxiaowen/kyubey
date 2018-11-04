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
using Andoromeda.Kyubey.Portal.Services;

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

            var dexs = db.Otcs.Where(x => x.Status == Status.Active).ToList();
            var bancors = db.Bancors.Where(x => x.Status == Status.Active).ToList();
            var hatchers = db.TokenHatchers.ToList();
            var banners = db.TokenBanners.ToList();

            var settings = new JsonSerializerSettings();
            settings.ContractResolver = new LowercaseContractResolver();

            foreach (var t in tokens)
            {
                var tokenFolder = System.IO.Path.Combine(rootTokensFolder, t.Id);

                if (!System.IO.Directory.Exists(tokenFolder))
                {
                    Directory.CreateDirectory(tokenFolder);
                }
                //manifest.json
                var filePath = System.IO.Path.Combine(tokenFolder, @"manifest.json");
                if (!System.IO.File.Exists(filePath))
                {
                    // Create a file to write to.
                    using (StreamWriter sw = System.IO.File.CreateText(filePath))
                    {
                        var tokenJObj = new TokenManifestJObject()
                        {
                            Id = t.Id,
                            Owners = new string[] { t.User.UserName },
                            Priority = 0,
                            Dex = dexs.Exists(x => x.Id == t.Id),
                            Incubation = hatchers.Where(x => x.TokenId == t.Id).Select(x => new IncubationJObject()
                            {
                                DeadLine = x.Deadline,
                                RaisedTarget = x.TargetCredits
                            }).FirstOrDefault(),
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
                                Tg = null,
                                Website = t.WebUrl
                            }
                        };
                        sw.Write(JsonConvert.SerializeObject(tokenJObj, Formatting.Indented, settings));
                    }
                }
                //contract_exchange
                {
                    var currentFolder = System.IO.Path.Combine(tokenFolder, @"contract_exchange");
                    if (!System.IO.Directory.Exists(currentFolder))
                    {
                        Directory.CreateDirectory(currentFolder);
                    }
                }
                //images
                {
                    var currentFolder = System.IO.Path.Combine(tokenFolder, @"images");
                    if (!System.IO.Directory.Exists(currentFolder))
                    {
                        Directory.CreateDirectory(currentFolder);
                    }
                }
                //incubator
                {
                    var currentFolder = System.IO.Path.Combine(tokenFolder, @"incubator");
                    if (!System.IO.Directory.Exists(currentFolder))
                    {
                        Directory.CreateDirectory(currentFolder);
                    }
                }
                //slides
                {
                    var currentFolder = System.IO.Path.Combine(tokenFolder, @"slides");
                    if (!System.IO.Directory.Exists(currentFolder))
                    {
                        Directory.CreateDirectory(currentFolder);
                    }
                }
                //updates
                {
                    var currentFolder = System.IO.Path.Combine(tokenFolder, @"updates");
                    if (!System.IO.Directory.Exists(currentFolder))
                    {
                        Directory.CreateDirectory(currentFolder);
                    }
                }

                //hatcher description.en.txt
                //hatcher detail.en.md
                {
                    var currentHatcher = hatchers.FirstOrDefault(x => x.TokenId == t.Id);
                    if (currentHatcher != null)
                    {
                        {
                            var currentFilePath = System.IO.Path.Combine(tokenFolder, @"incubator", @"description.en.txt");
                            if (!System.IO.File.Exists(currentFilePath))
                            {
                                using (StreamWriter sw = System.IO.File.CreateText(currentFilePath))
                                {
                                    sw.Write($@"{currentHatcher.Introduction}");
                                }
                            }
                        }
                        {
                            var currentFilePath = System.IO.Path.Combine(tokenFolder, @"incubator", @"detail.en.md");
                            if (!System.IO.File.Exists(currentFilePath))
                            {
                                using (StreamWriter sw = System.IO.File.CreateText(currentFilePath))
                                {
                                    sw.Write($@"{currentHatcher.Detail}");
                                }
                            }
                        }

                    }
                    //token info
                    else
                    {
                        {
                            var currentFilePath = System.IO.Path.Combine(tokenFolder, @"incubator", @"description.en.txt");
                            if (!System.IO.File.Exists(currentFilePath))
                            {
                                using (StreamWriter sw = System.IO.File.CreateText(currentFilePath))
                                {
                                    sw.Write($@"{t.Description}");
                                }
                            }
                        }
                        {
                            var currentFilePath = System.IO.Path.Combine(tokenFolder, @"incubator", @"detail.en.md");
                            if (!System.IO.File.Exists(currentFilePath))
                            {
                                using (StreamWriter sw = System.IO.File.CreateText(currentFilePath))
                                {
                                    sw.Write($@"{t.Description}");
                                }
                            }
                        }
                    }
                }



                //banner
                {
                    var currentObj = banners.Where(x => x.TokenId == t.Id).OrderBy(x => x.BannerOrder).ToList();
                    foreach (var c in currentObj)
                    {
                        var currentFilePath = System.IO.Path.Combine(tokenFolder, @"slides", c.BannerOrder + ".en.png");
                        if (c.Banner != null)
                        {
                            using (var bw = new BinaryWriter(System.IO.File.Open(currentFilePath, FileMode.OpenOrCreate)))
                            {
                                bw.Write(c.Banner);
                            }
                        }
                    }
                }
                //icon
                {
                    var currentFilePath = System.IO.Path.Combine(tokenFolder, "icon.png");
                    if (t.Icon != null)
                    {
                        using (var bw = new BinaryWriter(System.IO.File.Open(currentFilePath, FileMode.OpenOrCreate)))
                        {
                            bw.Write(t.Icon);
                        }
                    }
                }

                //exchange.js
                var exRow = bancors.FirstOrDefault(x => x.Id == t.Id)?.TradeJavascript;
                if (!string.IsNullOrWhiteSpace(exRow))
                {
                    var exchangejsFilePath = System.IO.Path.Combine(tokenFolder, "contract_exchange", @"exchange.js");
                    if (!System.IO.File.Exists(exchangejsFilePath))
                    {
                        using (StreamWriter sw = System.IO.File.CreateText(exchangejsFilePath))
                        {
                            sw.Write(exRow);
                        }
                    }
                }

                //price.js
                var priceRow = bancors.FirstOrDefault(x => x.Id == t.Id);
                if (priceRow != null)
                {
                    var priceFilePath = System.IO.Path.Combine(tokenFolder, "contract_exchange", @"price.js");
                    if (!System.IO.File.Exists(priceFilePath))
                    {
                        using (StreamWriter sw = System.IO.File.CreateText(priceFilePath))
                        {
                            sw.Write($@"{priceRow.CurrentBuyPriceJavascript}

{priceRow.CurrentSellPriceJavascript}");
                        }
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
