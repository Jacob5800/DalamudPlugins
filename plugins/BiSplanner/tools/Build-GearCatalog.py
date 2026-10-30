"""Build a compact, source-linked catalog from a user-authorized Discord handoff.
No Discord credentials or messages are included in the generated plugin resource.
Provider metadata is validated; ambiguous fight labels stay Unspecified.
"""
import argparse, concurrent.futures, datetime, hashlib, json, re, time
from pathlib import Path
from urllib.parse import urlsplit, parse_qs, unquote, quote, urlencode, urlunsplit
from urllib.request import Request, urlopen

FIGHTS = {
 'FRU': r'\bFRU\b|futures? rewritten', 'TOP': r'(?-i:\bTOP\b)|omega protocol',
 'DSR': r'\bDSR\b|dragonsong', 'TEA': r'\bTEA\b|epic of alexander',
 'UWU': r'\bUWU\b|weapon.?s refrain', 'UCOB': r'\bUCOB\b|unending coil|coils? of bahamut',
 'DMU': r'\bDMU\b|dancing mad', 'AMR': r'\bAMR\b|another mount rokkon|mount rokkon',
 'AAI': r'\bAAI\b|another aloalo|aloalo(?: island)?', 'ASS': r'\bASS\b|sil.dih',
 'Occult Crescent': r'occult crescent', 'Unreal': r'\bunreal\b'
}
MANUAL = {
'f83e8415-c192-4e91-84df-09c70a47f3d1':['FRU'],
'd1d8c2a1-da5b-4441-b047-5427ff4e8fe5':['DSR'],
'307bf5a3-d871-42f5-8095-ecd17b8ef823':['DSR'],
'6480ebbb-a180-4401-a7c9-650d253c65b7':['TOP'],
'9931b80b-57da-43b6-b70f-59bc96db8960':['TEA'],
'b824f085-d51f-4add-a3c2-718fb459ccd9':['TEA'],
'b85e6a8c-446e-45f8-bbf9-0c66267dc6c6':['Current raid'],
'121f8038-97f8-4049-a60e-ddc1ba1c9379':['Current raid'],
'9163946b-bf66-4f04-88e8-085c0de747c3':['Current raid'],
'98a28ada-28d0-42ac-af78-605bd4813272':['Current raid'],
'1a5555e9-c0da-44fd-bcef-655fb3892bd9':['UCOB'],
'167e3fe7-6f6d-438b-87bc-856b5cb852b7':['UCOB'],
'f7cd7e23-8e42-4ea8-bc45-5e4379f88d7e':['UWU'],
'd3ca3621-1144-49da-88eb-2e2cccfa675e':['UWU'],
'dcb3d409-c4b3-422a-9bd6-6f821e5a99eb':['Current raid'],
'fa98b375-e9ee-47f8-a5c0-1555b657e258':['FRU'],
'4c2ff1e7-b74d-4264-bffd-3968f9f825a0':['AMR','AAI'],
}
SUPPORTED = {'PLD','WAR','DRK','GNB','WHM','SCH','AST','SGE','MNK','DRG','NIN','SAM','RPR','VPR','BRD','MCH','DNC','BLM','SMN','RDM','PCT','BLU'}

def tags(text):
    text = re.sub(r'https?://\S+', '', text)
    # A weapon named after another fight is not an encounter assignment.
    text = re.sub(r'\b(FRU|TOP|DSR|TEA|UWU|UCOB)\s+(?:ultimate\s+)?weapon', '', text, flags=re.I)
    return [name for name, pattern in FIGHTS.items() if re.search(pattern, text, re.I)]

def provider(url):
    u = urlsplit(url); host = u.hostname or ''
    if host in ('xivgear.app','www.xivgear.app'):
        q = parse_qs(u.query); page = q.get('page',[''])[0]
        match = re.search(r'(?:^|\|)(?:sl)\|([a-f0-9-]{36})',unquote(page),re.I) or re.search(r'/sl/([a-f0-9-]{36})',u.path,re.I)
        if match: return ('xivgear',match.group(1).lower(),'https://api.xivgear.app/shortlink/'+match.group(1).lower())
        if 'bis' in page or '/bis/' in u.path or '/bis/' in u.fragment:
            normalized = urlunsplit((u.scheme,'xivgear.app',u.path.replace('//','/'),u.query,u.fragment))
            return ('xivgear',normalized,'https://api.xivgear.app/basedata/?url='+quote(normalized,safe=''))
    if host in ('etro.gg','www.etro.gg'):
        match = re.search(r'/gearset/([a-f0-9-]{36})',u.path,re.I)
        if match: return ('etro',match.group(1).lower(),'https://etro.gg/api/gearsets/'+match.group(1).lower()+'/')
    return None

def request_json(url):
    with urlopen(Request(url,headers={'User-Agent':'BiSplanner-Catalog/0.3 (+https://github.com/Jacob5800/DalamudPlugins)'}),timeout=35) as r:
        return json.load(r)

def validate_task(task, cache):
    key, url = task
    if key in cache: return key,cache[key]
    time.sleep(.25)
    try:
        j=request_json(url)
        return key,{'ok':True,'data':j}
    except Exception as ex: return key,{'ok':False,'error':str(ex)}

def identify(link, source, meta, set_data, key):
    if key in MANUAL: return MANUAL[key]
    u = unquote(link['url']).lower()
    for f in ('fru','top','dsr','tea','uwu','ucob'):
        if re.search(r'(?:ultimate[/|])'+f+r'(?:[/|&#?]|$)',u): return [f.upper()]
    if '/bis/rpr/current' in u: return ['Current raid','DMU']
    if re.search(r'(?:[/|])(?:current|prog)(?:[/|&#?]|$)',u): return ['Current raid']
    for text in (set_data.get('name',''), meta.get('name',''), link.get('label',''), source.get('title','')):
        ts=tags(text)
        if ts: return ts
    if re.search(r'\b(current|savage|prog|progression|best[ -]in[ -]slot)\b',meta.get('name','')+' '+link.get('label',''),re.I): return ['Current raid']
    # Nearby message excerpts can contain several unrelated links. They are
    # review evidence, not sufficient on their own to assign a fight.

    return ['Unspecified']

def indexed_url(url, index):
    # Keep existing query fields and fragments. Add onlySetIndex for sheet entries.
    u=urlsplit(url);q=parse_qs(u.query)
    if 'onlySetIndex' in q:return url
    return urlunsplit((u.scheme,u.netloc,u.path,u.query+('&' if u.query else '')+'onlySetIndex='+str(index),u.fragment))

def main():
    ap=argparse.ArgumentParser();ap.add_argument('handoff');ap.add_argument('output');ap.add_argument('--cache',required=True);a=ap.parse_args()
    handoff=json.loads(Path(a.handoff).read_text(encoding='utf-8-sig'));cp=Path(a.cache);cache=json.loads(cp.read_text(encoding='utf-8')) if cp.exists() else {}
    sources=handoff['sources'];tasks={};links=[]
    for source in sources:
        for link in source['links']:
            prov=provider(link['url'])
            if prov:
                kind,key,url=prov;tasks[kind+':'+key]=url;links.append((source,link,kind,key))
    print('Provider documents to validate:',len(tasks),flush=True)
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
        futures=[pool.submit(validate_task,t,cache) for t in tasks.items()]
        for count,f in enumerate(concurrent.futures.as_completed(futures),1):
            k,value=f.result();cache[k]=value
            if count%25==0 or count==len(tasks):
                cp.write_text(json.dumps(cache),encoding='utf-8');print('Validated',count,'/',len(tasks),flush=True)
    entries=[];errors=[];seen=set()
    for source,link,kind,key in links:
        observation=cache[kind+':'+key]
        if not observation['ok']:errors.append({'Url':link['url'],'Error':observation['error']});continue
        meta=observation['data'];job=meta.get('job')
        if isinstance(job,dict):job=job.get('abbreviation') or job.get('abbrev')
        if not isinstance(job,str):
            candidate=meta.get('jobAbbrev') or meta.get('jobAbbreviation')
            job=candidate
        if not isinstance(job,str) or job.upper() not in SUPPORTED:
            errors.append({'Url':link['url'],'Error':'Unsupported/unresolved provider job: '+str(job)});continue
        job=job.upper();resource=source.get('name','').replace('ew_','')
        channel=re.match(r'^([a-z]{3})_resources$',resource)
        if channel and channel.group(1).upper() in SUPPORTED and channel.group(1).upper()!=job:
            errors.append({'Url':link['url'],'Error':'Provider job '+job+' differs from channel '+resource});continue
        only=parse_qs(urlsplit(link['url']).query).get('onlySetIndex')
        sets=meta.get('sets')
        sets=list(enumerate(sets)) if isinstance(sets,list) else [(None,meta)]
        if only:
            try:sets=[(i,s) for i,s in sets if i==int(only[0])]
            except ValueError:sets=[]
        for i,st in sets:
            if kind=='xivgear' and not any(isinstance(v,dict) and isinstance(v.get('id'),int) and v['id']>0 for v in st.get('items',{}).values()):continue
            if kind=='etro' and not any(st.get(slot) for slot in ['weapon','head','body','hands','legs','feet']):continue
            set_job=st.get('jobOverride') or st.get('job') or job
            if not isinstance(set_job,str):set_job=job
            set_job=set_job.upper()
            if set_job not in SUPPORTED:continue
            fights=identify(link,source,meta,st,key) if set_job!='BLU' else ['Unspecified']
            archived=bool(source.get('archive'));community=job=='SCH' and 'legacy' in source.get('title','').lower()
            url=indexed_url(link['url'],i) if i is not None else link['url']
            name=st.get('name') or meta.get('name') or link['label']
            unique=(set_job,tuple(fights),kind,key,i,archived,community)
            if unique in seen:continue
            seen.add(unique)
            entries.append({'Job':set_job,'Fights':fights,'Name':name,'Label':link.get('label',''),'Url':url,'OriginalUrl':link['url'],'SourceUrl':link.get('source_url') or source['source_url'],'Source':'The Balance Discord: '+source.get('title',resource),'Historical':archived,'Community':community,'Validated':'2026-10-30','SheetName':meta.get('name','')})
    output={'Version':1,'Collected':'2026-10-30','Entries':entries}
    Path(a.output).parent.mkdir(parents=True,exist_ok=True);Path(a.output).write_text(json.dumps(output,indent=2,ensure_ascii=True)+'\n',encoding='utf-8')
    report={'ValidatedProviderDocuments':sum(v['ok'] for v in cache.values()),'Entries':len(entries),'Jobs':sorted({e['Job'] for e in entries}),'Historical':sum(e['Historical'] for e in entries),'Community':sum(e['Community'] for e in entries),'Unspecified':sum(e['Fights']==['Unspecified'] for e in entries),'Errors':list({e['Url']:e for e in errors}.values())}
    Path(a.output+'.validation.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8');print(json.dumps({k:v for k,v in report.items() if k!='Errors'}),flush=True)
if __name__=='__main__':main()
