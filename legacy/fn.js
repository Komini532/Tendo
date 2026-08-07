const $c = (text, lang) => "```" + `${lang || ""}\n${text}\n` + "```",
  code = $c;

module.exports = function(Discord, client) {
  let used = {};

  if (Discord && client) {
    let embed = e => new Discord.MessageEmbed(e);

    used = {
      collector: async (message, c = {}) => {
        return new Promise((resolve, reject) => {
          let user = c.user || message.author,
            channel = c.channel || message.channel,
            time = c.time || 180000;

          let C = channel.createMessageCollector(f => f.author == user, {
            time: time
          });

          let getCount = 0;
          C.on("collect", async m => {
            if (m.content == "x" || m.content == "0") {
              return C.stop();
            }

            getCount++;
            if (typeof c.collect == "function") {
              await c.collect(m);
            }
            if (getCount >= c.max) {
              C.stop();
            }
          });

          C.on("end", async col => {
            if (typeof c.collect == "function") {
              await c.end(col, $c(`- 処理終了`, "diff"));
            }
            resolve(col);
          });
        });
      },
      get: async (message, c = {}) => {
        return new Promise(async (resolve, reject) => {
          let user = c.user || message.author,
            channel = c.channel || message.channel,
            time = c.time || 30000,
            filter = c.filter || (f => f.author == user);

          let C = channel.createMessageCollector({
            filter: filter, 
            time: time
          });
          let M = c.content ? await channel.send({ embeds:[c.content] }) : null;

          C.on("collect", msg => {
            C.stop();
          });

          C.on("end", async col => {
            if (M) {
              if (c.delete) {
                await M.delete();
              } else {
                await M.edit({ content: $c(`- 処理終了`, "diff") });
              }
            }
            let colF = col.first();
            if (colF) {
              resolve(colF.content);
            } else {
              resolve("null");
            }
          });
        });
      },
      page: async (message, c = {}) => {
        return new Promise(async (resolve, reject) => {
          let pages = Array.isArray(c) ? c : c.pages || [],
            searchValue = c.searchValue || [],
            user = c.user || message.author,
            channel = c.channel || message.channel,
            time = c.time || 300000,
            datatype = c.datatype == "xy" ? "xy" : "x";

          let page = 1;
          let UI = () =>
            code(
              [
                `[p] <- [ Page ${page} / ${pages.length} ] -> [n]`,
                `[x] [0] で停止`
              ].join("\n"),
              "CSS"
            );
          let core = await channel.send({ content:UI(), embeds:[pages[0]] });
          let C = channel.createMessageCollector(f => f.author == user);
          let P = { pause: false };
          let L = pages[0];
          let Rs = [];

          C.on("collect", async msg => {
            switch (msg.content) {
              case "x":
              case "0":
                C.stop();
                return;
            }
            if (P.pause) return;
            if (typeof c.collect == "function") {
              let R = await c.collect();
              Rs.push(R);
            }
            switch (msg.content) {
              case "p":
                page += 1;
                break;
              case "n":
                page -= 1;
                break;
              default:
                let pn = parseInt(msg.content);
                if (!isNaN(pn) && isFinite(pn)) {
                  page = pn;
                }
            }
            if (page > pages.length) page = 1;
            if (page < 0) page = pages.length;

            let E = pages[page - 1];
            L = E;
            if (E) {
              await core.edit({ content: UI(), embeds:[E] });
            }
          });

          C.on("end", async col => {
            await core.edit({ content: $c(`- 処理終了`, "diff"), embeds:[L] });
            resolve(col, Rs, L);
          });
        });
      },
      expage: (message, c = {}) => {
        return new Promise(async (resolve, reject) => {
          let user = c.user || message.author,
            channel = c.channel || message.channel,
            time = c.time || 120000,
            contents = c.contents || [],
            partSet = c.partSet || { part:x => $c(x,""), length:1, frame:null, border:null },
            filterSet = c.filterSet || null,
            resByIndex = c.resByIndex, resByPageNumber = c.resByPageNumber;
          
          let part = Array.isArray(partSet.part) ? partSet.part[0] : partSet.part,
              partFrame = partSet.frame,
              partBorder = partSet.border,
              length = Array.isArray(partSet.length) ? partSet.length[0] : partSet.length;
          
          let noFilter = !filterSet,
              filter1, filter2, filter3, filterName = ["名前フィルター", "種類フィルター"],
              defaultFilter;
          if( filterSet ){
            filter1 = filterSet.filter1;
            filter2 = filterSet.filter2;
            filter3 = filterSet.filter3;
            filterName = filterSet.filterName;
            defaultFilter = filterSet.defaultFilter;
          }
          
          // MODE=0
          let page = 1;
          let pagemax = Math.ceil(contents.length / length);
          let vrad = "abcdefghij".split("");
          let clist = contents.slice(0, length);
          
          // MODE=1
          let option = 0;
          let config = {
            hyouji: 0,
            filter1: "",
            filter2: "",
            filter3: "",
          };
          
          if( defaultFilter ){
            config[ defaultFilter.name ] = defaultFilter.value;
          }
          
          let MODE = 0;
          let cursor = (length, index) => {
            let a = [];
            for( var i=0;i<length;i++ ){
              a.push(`${i==index ? "[" : " "}${i+1}${i==index ? "]" : " "}`);
            }
            return a.join(" ");
          };
          let UI1 = () => {
            return code([
              `「数字」「p/n」...操作 「a～${vrad[length-1]}」...決定`,
              `「${noFilter ? "-" : "q"}」...設定 「0」「x」...終了`,
              `[p] <- [${page} / ${pagemax}] -> [n]`,
            ].join("\n"), "css");
          };
          let UI2 = () => {
            if( MODE==0 ){
              if( Array.isArray(part) ) part = partSet.part[config.hyouji];
              if( Array.isArray(length) ) length = partSet.length[config.hyouji];
              contents = c.contents;
              
              if( config.filter1 && filter1 ){
                contents = contents.filter(f => filter1(f, config.filter1));
              }
              if( config.filter2 && filter2 ){
                contents = contents.filter(f => filter2(f, config.filter2));
              }
              if( config.filter3 && filter3 ){
                contents = contents.filter(f => filter3(f, config.filter3));
              }
              pagemax = Math.ceil(contents.length / length);
              
              let tpage = page - 1;
              clist = contents.slice(tpage * length, (tpage+1) * length);

              let build = [];
              for( var i=0;i<clist.length;i++ ){
                build.push(part(clist[i],vrad[i],i));
              }

              return embed({
                description: (partFrame ? partFrame(build.join(partBorder || "")) : build.join(partBorder || "")) || code("- 該当するものがありません。","diff"),
              });
            }else if( MODE==1 ){
              return embed({
                description: code(`+*+*+ OPTIONS +*+*+`, "DIFF") + code([
                  `${option==0 ? ">" : " "} [a] 表示方式`,
                  Array.isArray(partSet.part) ? `  ${cursor(partSet.part.length,config.hyouji)}` : "　使用できません",
                  `${option==1 ? ">" : " "} [b] ${filterName[0]}`,
                  filter1 ? `  条件："${config.filter1}"` : "　使用できません",
                  `${option==2 ? ">" : " "} [c] ${filterName[1]}`,
                  filter2 ? `  条件："${config.filter2}"` : "　使用できません",
                  `${option==3 ? ">" : " "} [d] 決定`,
                  `${option==4 ? ">" : " "} [e] リセット`,
                ].join("\n"), "CSS"),
              });
            }
          };
          
          let core = await channel.send({ content:UI1(), embeds:[ UI2() ] });
          let C = channel.createMessageCollector({ filter: f => f.author == user, time:time });
          let R;
          
          C.on("collect", async msg => {
            let content = msg.content;
            if( c.submitkey == content ) content = "a";
            
            let epage = parseInt(content);
            let vpage = vrad.indexOf(content);
            
            if( msg.content=="0" || msg.content=="x" ){
              C.stop();
            }else if( MODE==0 ){
              if( msg.content=="p" ) epage = epage - 1;
              if( msg.content=="n" ) epage = epage + 1;

              if( msg.content=="q" && !noFilter ){
                MODE = 1;
                option = 0;
                let u2 = UI2();
                await core.edit({ content: UI1(), embeds:[ u2 ] });
              }else if( vpage!=-1 ){
                if( resByIndex ){
                  R = vpage;
                }else if( resByPageNumber ){
                  R = page - 1;
                }else{
                  R = clist[vpage];
                }
                C.stop();
              }else if( !isNaN(epage) && isFinite(epage) ){
                if( epage <= 1 ) epage = 1;
                if( epage >= pagemax ) epage = pagemax;

                page = epage;

                let u2 = UI2();
                await core.edit({ content: UI1(), embeds:[ u2 ] });
              }
            }else if( MODE==1 ){
              let edit = false;
              if( vpage!=-1 ){
                option = vpage;
                if( option>=5 ) option = 4;
                if( option<=0 ) option = 0;
                edit = true;
              }
              switch(option){
                case 0:
                  switch(epage){
                    case 1:
                    case 2:
                      config.hyouji = epage-1;
                      break;
                  }
                  break;
                case 1:
                  if( !edit && filter1 ){
                    config.filter1 = msg.content;
                  }
                  break;
                case 2:
                  if( !edit && filter2 ){
                    config.filter2 = msg.content;
                  }
                  break;
                case 3:
                  MODE = 0;
                  break;
                case 4:
                  config.hyouji = 0;
                  config.filter1 = "";
                  config.filter2 = "";
                  option = 0;
                  break;
              }
              let u2 = UI2();
              await core.edit({ content: UI1(), embeds:[ u2 ] });
            }
          });
          
          C.on("end", async col => {
            let u2 = UI2();
            if( c.afterDelete ){
              await core.delete();
            }else{
              await core.edit({ content: $c(`- 処理終了`, "diff"), embeds:[ u2 ] });
            }
            resolve(R);
          });
        });
      },
      selector: (message, c = {}, multiple) => {
        return new Promise(async (resolve, reject) => {
          let user = c.user || message.author,
            channel = c.channel || message.channel,
            time = c.time || 30000,
            title = c.title || "";
          let select = Array.isArray(c) ? c : c.select || [];

          let I = "abcdefghijklmnopqrstuvwxyz".split("");
          let C = channel.createMessageCollector({
            filter: f => f.author == user, 
            time: time
          });
          let L1 = -1,
            L2 = [],
            L = multiple ? L2 : L1;
          let core = await channel.send({ embeds: [
            embed({
              description: title + select
                .map((m, i) => `:regional_indicator_${I[i]}: \`${m}\``)
                .join("\n"),
              footer: {
                text: `文字を入力して選択してください。${
                  multiple ? "（複数選択可）" : "（１つのみ）"
                }`
              }
            })
          ]});

          C.on("collect", msg => {
            switch (msg.content) {
              case "x":
              case "0":
                L = multiple ? L2 : L1;
                C.stop();
                return;
            }

            let R;
            if (multiple) {
              let S = msg.content.split(/\s+/).map(s => I.indexOf(s));
              let Sr = S.filter(f => f != -1);
              if (Sr.length) R = Sr;
            } else {
              let Index = I.indexOf(msg.content);
              if (Index != -1) R = Index;
            }
            
            console.log(R);

            if ( Array.isArray(R) || Number(R)>=0 ) {
              L = R;
              C.stop();
            }
          });

          C.on("end", async () => {
            await core.delete();
            resolve(L);
          });
        });
      },
    };
  }

  let unused = {
    s: (obj, key) => {
      if (Array.isArray(key)) {
        let browse = obj;
        for (var i = 0; i < key.length; i++) {
          browse = browse[key[i]];
          if (!browse) return false;
        }
        if (browse) {
          return browse;
        } else {
          return false;
        }
      } else if (typeof key == "string") {
        return obj[key];
      } else {
        return false;
      }
    },
    random: (a, b, f) => {
      if (Array.isArray(a)) {
        return a[Math.floor(Math.random() * a.length)];
      } else {
        if( f ){
          let c = b - a,
            d = Math.random() * c,
            e = d + a;
          return e;
        }else{
          let c = b - a,
            d = Math.round(Math.random() * c),
            e = d + a;
          return e;
        }
      }
    },
    range: (a, b, c) => {
      let Ta = Number(a),
        Tb = b[1] || c,
        Tc = b[0] || b;
      if (Tb === null) Tb = Infinity;
      if (Tc === null) Tc = -Infinity;

      if (!isNaN(a)) {
        if (Tb >= Ta && Tc <= Ta) {
          return true;
        }
      }
      return false;
    },
    separate: (a, b) => {
      b = Number(b) || 1;
      return String(a).match(new RegExp(`.{1,${b}}`, "g"));
    },
    delay: async function(time){
      return new Promise((res) => {
        setTimeout(() => {
          res();
        }, time);
      })
    },
    eff: (data, req, A, B, reqfn=(d,a,b,c)=>d[a]==b) => {
      switch(req){
        case "readonly":
          if( true ){
            let sp1 = String(data.eff).split("|");
            let sp2 = sp1.map(s1 => s1.split(","));
            
            return sp2;
          }
          return null;
        case "load":
          if( true ){
            let sp1 = String(data.eff).split("|");
            let sp2 = sp1.map(s1 => s1.split(","));
            
            data.eff = sp2;
            
            return sp2;
          }
          return null;
        case "save":
          if( true ){
            let sp1 = data.eff.map(s1 => s1.join(","));
            let sp2 = sp1.join("|");
            
            data.eff = sp2;
            
            return sp2;
          }
          return null;
        case "read":
          if( true ){
            let find = data.eff.find(d => reqfn(d, A, B));
            
            return find || null;
          }
          return null;
        case "write":
          if( true ){
            let find1 = data.eff.findIndex(d => reqfn(d, A, B));
            let find2 = data.eff[find1];
            let b = [find2[0]].concat(A);
            
            data.eff[find1] = b;
            
            return b || null;
          }
          return null;
        case "new":
          if( true ){
            let obj = [].concat(A,B);
            let already = data.eff.find(d => d[0]==obj[0] && d[2]==obj[2]);
            
            if( !already ){
              data.eff.push(obj);

              return obj || null;
            }
          }
          return null;
        case "delete":
        case "remove":
          if( true ){
            let find1 = data.eff.findIndex(d => reqfn(d, A, B));
            let filter = data.eff.filter((d,i,a) => d == a[find1]);
            
            data.eff = filter;
            
            return filter || null;
          }
          return null;
        case "filter":
          if( true && typeof A == "function" ){
            let filter = data.eff.filter((d,i,a) => A(d,i,a));
            
            data.eff = filter;
            
            return filter || null;
          }
          return null;
        case "each":
          if( true && typeof A == "function" ){
            data.eff = data.eff.map(d => {              
              return A(d);
            });
            
            return null;
          }
          return null;
        case "import":
          if( true ){
            data.eff = A.filter(f => f[0]);
          }
          return null;
        case "export":
          if( true ){
            return data.eff.filter(f => f[0]);
          }
          return null;
        default:
          return null;
      }
    },
    meta: (data, req, A, B, reqfn=(d,a,b)=>d.id==a) => {
      switch(req){
        case "readonly":
          if( true ){
            let spz = {};
            let sp1 = String(data.meta).split("|");
            let sp2 = sp1.map(s1 => {
              let sp2_1 = s1.split(":");
              let sp2_2 = sp2_1[1] ? sp2_1[1].split(",") : [];
              
              spz[sp2_1[0]] = sp2_2;
            });
            
            return spz;
          }
          return null;
        case "load":
          if( true ){
            let sp1 = String(data.meta).split("|");
            let sp2 = sp1.map(s1 => {
              let sp2_1 = s1.split(":");
              let sp2_2 = sp2_1[1] ? sp2_1[1].split(",") : [];
              
              return {
                id: sp2_1[0],
                value: sp2_2,
              }
            });
            
            data.meta = sp2;
            return sp2;
          }
          return null;
        case "save":
          if( true ){
            let sp1 = data.meta.map(s1 => {
              return [s1.id, s1.value.join(",")].join(":");
            });
            let sp2 = sp1.join("|");
            
            data.meta = sp2;
          }
          return null;
        case "read":
          if( true ){
            let find = data.meta.find(d => reqfn(d,A,B));
            
            let res = find ? find.value : [];
            
            if( res.length<=1 ){
              return res[0] || null;
            }else{
              return res;
            }
          }
          return null;
        case "write":
          if( true ){
            let find1 = data.meta.findIndex(d => reqfn(d, A, B,));
            
            let b = data.meta[find1];
            b.value = B;

            return b || null;
          }
          return null;
        case "new":
          if( true ){
            let obj = typeof A=="object" ? A : {
              id: A,
              value: B,
            };
            data.meta.push(obj);

            return obj || null;
          }
          return null;
        case "delete":
          if( true ){
            let filter = data.meta.filter((d,i,a) => !reqfn(d, A, B));
            
            data.meta = filter;
            
            return filter || null;
          }
          return null;
        case "remove":
          if( true ){
            let find1 = data.meta.findIndex(d => reqfn(d, A, B,));
            let filter = data.meta.filter((d,i,a) => i != find1);
            
            data.meta = filter;
            
            return filter || null;
          }
          return null;
        default:
          return null;
      }
    },
  };

  return Object.assign(used, unused);
};
