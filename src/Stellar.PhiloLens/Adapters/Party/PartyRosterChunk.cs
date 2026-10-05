using System.Globalization;

namespace Stellar.PhiloLens.Adapters.Party;

/// <summary>Builds the Lua chunk that reads the party roster the game already loaded for the open profile card.
/// <para><b>Read-only.</b> The chunk never calls a game function that could reach the server: it asks the UI for
/// the open <c>idcard</c> view and then only walks plain Lua tables (<c>next</c>/<c>rawget</c>, no metamethods).
/// The card's profile data is the game's decoded <c>Social.GetSocialData</c> reply (camelCase field names);
/// its <c>teamData.&lt;member map&gt;</c> holds every member's <c>TeamMemData</c>.</para>
/// <para>Where the view keeps that table, and the names of a few fields, aren't known yet, so the chunk finds
/// them by shape: the profile table is the one under the view with this player's <c>charId</c> and a
/// <c>basicData</c>; the member map is the <c>teamData</c> table whose values carry a <c>socialData</c>; the
/// stats table is the one holding <c>fightPoint</c> (next to <c>seasonStrength</c>, confirmed in-game). It reports
/// the field names it saw so a test run can pin the rest.</para>
/// <para>Output, one line each, tab-separated, in <see cref="ResultGlobal"/>:
/// <c>philo-lens/1, charId, status</c> (status: nocard, pending, solo, party, shape, error) ·
/// <c>P, abilityScore, seasonStrength</c> (the inspected player's own stats, from the same profile data; solo
/// players too) · <c>T, key=value…</c> (teamData's plain fields) · <c>O, charId…</c> (the party order, when found) ·
/// <c>A, key…</c> (the stats table's field names) · <c>V, key:type…</c> (the card view's fields, when the
/// profile table wasn't found under it) ·
/// <c>M, charId, roleId, classId, abilityScore, seasonStrength, enterTime, groupId, name</c> per member
/// (<c>groupId</c> is the member's raid group, 1 to 4; always 1 in a regular party).</para></summary>
internal static class PartyRosterChunk
{
    public const string ResultGlobal = "__philo_lens_party";
    public const string Header = "philo-lens/1";

    private const string CharIdToken = "__CHAR__";

    // MAX_DEPTH/MAX_STEPS bound the search under the card view (tables deep, fields visited in total), so a view
    // holding unexpectedly large tables can't stall a frame.
    private const string Chunk = """
        rawset(_G, '__philo_lens_party', nil)
        local ok, err = pcall(function()
          local CHAR = '__CHAR__'
          local MAX_DEPTH, MAX_STEPS = 3, 20000
          local out = { 'philo-lens/1\t' .. CHAR .. '\tpending' }
          local function put(line) out[#out + 1] = line end
          local function status(s) out[1] = 'philo-lens/1\t' .. CHAR .. '\t' .. s end
          local function text(v)
            if v == nil then return '' end
            if type(v) == 'number' then return string.format('%.0f', v) end
            if type(v) == 'string' then return (string.gsub(v, '%c', ' ')) end
            -- LuaJIT prints a 64-bit integer as e.g. 123LL; only those lose the suffix, never a name.
            return (string.gsub(string.gsub(tostring(v), 'U?LL$', ''), '%c', ' '))
          end
          local function findProfile(root)
            local queue, depth, head, steps = { root }, { [root] = 0 }, 1, 0
            while head <= #queue and steps < MAX_STEPS do
              local t = queue[head]
              head = head + 1
              if type(rawget(t, 'basicData')) == 'table' and text(rawget(t, 'charId')) == CHAR then return t end
              if depth[t] < MAX_DEPTH then
                local k, v = next(t)
                while k ~= nil and steps < MAX_STEPS do
                  steps = steps + 1
                  if type(v) == 'table' and depth[v] == nil then
                    depth[v] = depth[t] + 1
                    queue[#queue + 1] = v
                  end
                  k, v = next(t, k)
                end
              end
            end
          end
          local function findStats(social)
            local k, v = next(social)
            while k ~= nil do
              if type(v) == 'table' and rawget(v, 'fightPoint') ~= nil then return v end
              k, v = next(social, k)
            end
          end
          local function keysOf(t, withType)
            local keys, k, v = {}, next(t)
            while k ~= nil do
              keys[#keys + 1] = withType and (text(k) .. ':' .. type(v)) or text(k)
              k, v = next(t, k)
            end
            return table.concat(keys, '\t')
          end

          local function putMembers(members)
            local statsKeysPut = false
            local mk, m = next(members)
            while mk ~= nil do
              if type(m) == 'table' then
                local social = rawget(m, 'socialData') or {}
                local basic = rawget(social, 'basicData') or {}
                local profession = rawget(social, 'professionData') or {}
                local stats = findStats(social) or {}
                if not statsKeysPut and next(stats) ~= nil then
                  put('A\t' .. keysOf(stats))
                  statsKeysPut = true
                end
                put(table.concat({ 'M', text(rawget(m, 'charId') or mk), text(rawget(m, 'talentId')),
                  text(rawget(profession, 'professionId')), text(rawget(stats, 'fightPoint')), text(rawget(stats, 'seasonStrength')),
                  text(rawget(m, 'enterTime')), text(rawget(m, 'groupId')), text(rawget(basic, 'name')) }, '\t'))
              end
              mk, m = next(members, mk)
            end
          end
          local function readTeam(team)
            local members, order, scalars, hasValue = nil, nil, {}, false
            local k, v = next(team)
            while k ~= nil do
              if type(v) == 'table' then
                local _, first = next(v)
                if type(first) == 'table' and rawget(first, 'socialData') ~= nil then members = v
                elseif rawget(v, 1) ~= nil and type(rawget(v, 1)) ~= 'table' then order = v end
              else
                scalars[#scalars + 1] = text(k) .. '=' .. text(v)
                if v ~= 0 and v ~= false and v ~= '' then hasValue = true end
              end
              k, v = next(team, k)
            end
            put('T\t' .. table.concat(scalars, '\t'))
            if members == nil then status(hasValue and 'shape' or 'solo') return end
            if order ~= nil then
              local ids = {}
              for i = 1, #order do ids[i] = text(order[i]) end
              put('O\t' .. table.concat(ids, '\t'))
            end
            putMembers(members)
            status('party')
          end
          local function read()
            local view = Z.UIMgr:GetView('idcard')
            if type(view) ~= 'table' or text(view.cardId_) ~= CHAR then status('nocard') return end
            local profile = findProfile(view)
            if profile == nil then put('V\t' .. keysOf(view, true)) return end
            local own = findStats(profile) or {}
            put('P\t' .. text(rawget(own, 'fightPoint')) .. '\t' .. text(rawget(own, 'seasonStrength')))
            local team = rawget(profile, 'teamData')
            if type(team) ~= 'table' then status('solo') return end
            readTeam(team)
          end

          read()
          rawset(_G, '__philo_lens_party', table.concat(out, '\n'))
        end)
        if not ok then rawset(_G, '__philo_lens_party', 'philo-lens/1\t__CHAR__\terror\t' .. tostring(err)) end
        """;

    public static string For(long charId) => Chunk.Replace(CharIdToken, charId.ToString(CultureInfo.InvariantCulture));
}
