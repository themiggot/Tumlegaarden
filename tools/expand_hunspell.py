# Bruges til at lave src/Tumlegaarden.Web/GameData/ord-gyldige.txt:
#   python3 tools/expand_hunspell.py /usr/share/hunspell/da_DK.aff /usr/share/hunspell/da_DK.dic kandidater.txt
#   hunspell -d /usr/share/hunspell/da_DK -i utf-8 -a < kandidater.txt  (behold linjer med * + -)
# Udfolder hunspell-ordbog med FLAG num (komma-separerede talflag) til alle bøjede former.
import re, sys
aff_path, dic_path, out_path = sys.argv[1:4]
rules = {}      # flag -> (kind, cross, [(strip, add, contflags, regex)])
needaffix = onlyincompound = None
lines = open(aff_path, encoding='utf-8').read().split('\n')
i = 0
while i < len(lines):
    parts = lines[i].split()
    if len(parts) >= 2 and parts[0] == 'NEEDAFFIX': needaffix = parts[1]
    if len(parts) >= 2 and parts[0] == 'ONLYINCOMPOUND': onlyincompound = parts[1]
    if len(parts) == 4 and parts[0] in ('SFX', 'PFX') and parts[3].isdigit():
        kind, flag, cross, n = parts[0], parts[1], parts[2] == 'Y', int(parts[3])
        entries = []
        for j in range(1, n + 1):
            p = lines[i + j].split()
            strip = '' if p[2] == '0' else p[2]
            add, _, cont = p[3].partition('/')
            add = '' if add == '0' else add
            cond = p[4] if len(p) > 4 else '.'
            rx = re.compile(('^' + cond) if kind == 'PFX' else (cond + '$'))
            entries.append((strip, add, set(cont.split(',')) if cont else set(), rx))
        rules.setdefault(flag, (kind, cross, []))[2].extend(entries)
        i += n
    i += 1

def apply(word, flags, kinds=('SFX', 'PFX')):
    out = []
    for f in flags:
        if f not in rules: continue
        kind, cross, entries = rules[f]
        if kind not in kinds: continue
        for strip, add, cont, rx in entries:
            if kind == 'SFX':
                if not rx.search(word) or (strip and not word.endswith(strip)): continue
                new = (word[:-len(strip)] if strip else word) + add
            else:
                if not rx.search(word) or (strip and not word.startswith(strip)): continue
                new = add + (word[len(strip):] if strip else word)
            out.append((new, cont, kind, cross))
    return out

words = set()
for line in open(dic_path, encoding='utf-8').read().split('\n')[1:]:
    if not line.strip(): continue
    token = line.split()[0]                       # ordet + evt. /flag; morfologi efter mellemrum ignoreres
    w, _, fl = token.partition('/')
    flags = set(fl.split(',')) if fl else set()
    if onlyincompound in flags: continue
    if needaffix not in flags: words.add(w)
    for new, cont, kind, cross in apply(w, flags):
        if onlyincompound in cont: continue
        if needaffix not in cont: words.add(new)
        for new2, cont2, kind2, _ in apply(new, cont):          # anden bøjningsrunde (fortsættelsesflag)
            if onlyincompound not in cont2 and needaffix not in cont2: words.add(new2)
        if kind == 'SFX' and cross:                              # krydsprodukt med præfiks
            for new3, cont3, kind3, cross3 in apply(new, flags, ('PFX',)):
                if cross3 and onlyincompound not in cont3: words.add(new3)
five = sorted(w for w in words if re.fullmatch(r'[a-zæøå]{5}', w))
open(out_path, 'w', encoding='utf-8').write('\n'.join(five) + '\n')
print(len(words), 'former,', len(five), 'på fem bogstaver')
