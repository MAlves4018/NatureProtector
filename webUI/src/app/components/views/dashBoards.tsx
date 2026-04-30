import { useState} from 'react';
import {
  Search, Flame, AlertTriangle, Grid3X3, Layers, Leaf, Sun, Moon, TreePine, Mountain,
} from 'lucide-react';
import {
  Box, Flex, HStack, VStack, Text, Input, Checkbox,
} from '@chakra-ui/react';
import { GrafanaStrip } from '../mainComponents/GrafanaStrip';
import { PortugalMap } from '../mainComponents/PortugalMap';
import {useColors} from '../../utils/utils'

import { GridInfo} from "../../types"
import { useParams } from 'react-router-dom';

// ─── Danger zones ──────────────────────────────────────────────────────────────
const dangerZones = []

const gridCenters : GridInfo[] =
//getGrids()
[]

// ─── Segmented control helper ──────────────────────────────────────────────────
function SegButton({
  active, onClick, children, c,
}: { active: boolean; onClick: () => void; children: React.ReactNode; c: ReturnType<typeof useColors> }) {
  return (
    <Box
      as="button" onClick={onClick} flex={1}
      px={2} py="5px" borderRadius="md"
      fontSize="xs" fontWeight={active ? 'semibold' : 'normal'}
      color={active ? c.textPrimary : c.textSecond}
      bg={active ? c.segActive : 'transparent'}
      boxShadow={active ? 'sm' : 'none'}
      border={active ? `1px solid ${c.cardBorder}` : '1px solid transparent'}
      cursor="pointer"
      transition="all 0.15s"
      style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '5px' }}
    >
      {children}
    </Box>
  );
}

// ─── Main Page ─────────────────────────────────────────────────────────────────
export function DashBoards() {
  const {areaId: areaIdParam} = useParams<{ areaId: string }>();
  const currentArea = areaIdParam || '';

  const [isDark, setIsDark]               = useState(false);
  const c = useColors(isDark);

  return (
    <Flex direction="column" minH="100vh" bg={c.pageBg} transition="background 0.2s">

      {/* ── Navbar ── */}
      <Flex
        as="nav" align="center" gap={3} px={6} py="10px"
        bg={c.navBg} borderBottom="1px solid" borderColor={c.navBorder}
        shadow="sm" flexShrink={0} transition="background 0.2s"
      >
        <Flex align="center" justify="center" w="36px" h="36px" borderRadius="full" border="2px solid #16a34a">
          <Leaf size={18} color="#16a34a" />
        </Flex>
        <Text fontWeight="semibold" fontSize="lg" color={c.textPrimary} letterSpacing="wide">
          Nature Protector
        </Text>
        <Box flex={1} />
        <Box
          as="button" onClick={() => setIsDark(d => !d)}
          display="flex" alignItems="center" gap="6px"
          px={3} py="6px" borderRadius="full"
          bg={c.toggleBg} border="1px solid" borderColor={c.navBorder}
          cursor="pointer" transition="all 0.2s" _hover={{ opacity:0.8 }}
        >
          {isDark
            ? <><Sun size={14} color="#f59e0b"/><Text fontSize="xs" color={c.textSecond}>Light</Text></>
            : <><Moon size={14} color="#6366f1"/><Text fontSize="xs" color={c.textSecond}>Dark</Text></>
          }
        </Box>
      </Flex>

      <GrafanaStrip isDark={isDark} areaId={currentArea} {...c}/>

    </Flex>
  );
}

function MainBody() {
  const [isDark, setIsDark]               = useState(false);
  const c = useColors(isDark);

  const [showGrid, setShowGrid]                   = useState(false);
  const [showColorByDanger, setShowColorByDanger] = useState(true);
  const [showForests, setShowForests]             = useState(false);
  const [mapType, setMapType]                     = useState<MapType>('standard');
  const [searchQuery, setSearchQuery]             = useState('');
  const [showDanger, setShowDanger]               = useState(true);
  const [showSomeRisk, setShowSomeRisk]           = useState(true);
  const [showLittleRisk, setShowLittleRisk]       = useState(true);

  const filteredZones = dangerZones.filter((z) => {
    if (searchQuery.trim() && !z.name.toLowerCase().includes(searchQuery.toLowerCase())) return false;
    if (!showDanger    && z.danger > 75)               return false;
    if (!showSomeRisk  && z.danger > 60 && z.danger <= 75) return false;
    if (!showLittleRisk && z.danger <= 60)             return false;
    return true;
  });

  const iconItems = [
    { key:'danger', checked:showDanger,    onChange:setShowDanger,    hex:'#ef4444', Icon:Flame,         label:'Danger of wildfire'  },
    { key:'some',   checked:showSomeRisk,  onChange:setShowSomeRisk,  hex:'#f59e0b', Icon:AlertTriangle, label:'Some risk of wildfire' },
    { key:'little', checked:showLittleRisk,onChange:setShowLittleRisk,hex:'#22c55e', Icon:AlertTriangle, label:'Little risk of wildfire' },
  ];

  const dangerRanges = [
    { range:'0 – 60',   label:'Baixo',    hex:'#22c55e' },
    { range:'61 – 75',  label:'Moderado', hex:'#f59e0b' },
    { range:'76 – 100', label:'Elevado',  hex:'#ef4444' },
  ];

  const zoneCounts = [
    { label:'alto',   hex:'#ef4444', filter:(z:Zone)=>z.danger>75 },
    { label:'moder.', hex:'#f59e0b', filter:(z:Zone)=>z.danger>60&&z.danger<=75 },
    { label:'baixo',  hex:'#22c55e', filter:(z:Zone)=>z.danger<=60 },
  ];
  return (
     <>   
      <Flex flex={1} overflow="hidden" minH="500px">

        {/* Left Panel */}
        <Flex
          direction="column" w="280px" flexShrink={0}
          bg={c.panelBg} borderRight="1px solid" borderColor={c.panelBorder}
          overflowY="auto" transition="background 0.2s"
        >

          {/* Search */}
          <Box p={4} borderBottom="1px solid" borderColor={c.panelBorder}>
            <Text fontSize="sm" color={c.textSecond} mb={2}>Search:</Text>
            <Box position="relative">
              <Box position="absolute" left={3} top="50%" transform="translateY(-50%)" pointerEvents="none" zIndex={1}>
                <Search size={13} color={c.textMuted}/>
              </Box>
              <Input
                value={searchQuery} onChange={e => setSearchQuery(e.target.value)}
                placeholder="Pesquisar localização..." size="sm" pl="32px"
                bg={c.inputBg} borderColor={c.inputBorder} color={c.textPrimary}
                borderRadius="lg" _placeholder={{ color:c.textMuted }}
                _focus={{ borderColor:'#16a34a', boxShadow:'0 0 0 2px rgba(22,163,74,0.25)' }}
                transition="background 0.2s"
              />
            </Box>
          </Box>

          {/* Map Layer */}
          <Box p={4} borderBottom="1px solid" borderColor={c.panelBorder}>
            <Text fontSize="sm" color={c.textSecond} mb={3}>Map Layer:</Text>

            {/* Segmented map type */}
            <Flex
              bg={c.segBg} borderRadius="lg" p="3px" gap="2px"
              border="1px solid" borderColor={c.cardBorder} mb={3}
            >
              <SegButton active={mapType==='standard'} onClick={()=>setMapType('standard')} c={c}>
                <Layers size={11}/> Standard
              </SegButton>
              <SegButton active={mapType==='terrain'} onClick={()=>setMapType('terrain')} c={c}>
                <Mountain size={11}/> DEM
              </SegButton>
            </Flex>

            {mapType==='terrain' && (
              <Box
                bg={isDark?'#1a2e1a':'#f0fdf4'} borderRadius="md" px={3} py={2}
                border="1px solid" borderColor={isDark?'#166534':'#bbf7d0'} mb={1}
              >
                <Text fontSize="xs" color={isDark?'#86efac':'#15803d'}>
                  🏔 Visualização DEM/Topográfica — mostra relevo e curvas de nível de Portugal.
                </Text>
              </Box>
            )}

            {/* Forests toggle */}
            <Flex align="center" gap={3} cursor="pointer" as="label" mt={3}>
              <Checkbox.Root checked={showForests} onCheckedChange={e=>setShowForests(!!e.checked)} size="sm">
                <Checkbox.HiddenInput/>
                <Checkbox.Control style={{
                  borderColor: showForests ? '#16a34a' : c.inputBorder,
                  background:  showForests ? '#16a34a' : 'transparent',
                  transition: 'all 0.15s',
                }}/>
              </Checkbox.Root>
              <HStack gap={2}>
                <TreePine size={14} color="#16a34a"/>
                <Text fontSize="sm" color={c.textPrimary}>Florestas / Vegetação</Text>
              </HStack>
            </Flex>

            {showForests && (
              <Box mt={2} ml={7}>
                {/* Copernicus source badge */}
                <Box
                  bg={isDark?'#0d1f12':'#f0fdf4'} borderRadius="md" px={2} py="6px" mb={2}
                  border="1px solid" borderColor={isDark?'#166534':'#bbf7d0'}
                >
                  <HStack gap="6px" mb="3px">
                    <Box w="8px" h="8px" borderRadius="full" bg="#16a34a" flexShrink={0}/>
                    <Text fontSize="10px" color={isDark?'#86efac':'#15803d'} fontWeight="semibold">
                      Copernicus · EEA DiscoMap
                    </Text>
                  </HStack>
                  <Text fontSize="10px" color={isDark?'#6ee7b7':'#166534'}>
                    CORINE Land Cover 2018 — WMS
                  </Text>
                </Box>
                <Text fontSize="xs" color={c.textMuted}>Clique numa área para ver detalhes.</Text>
              </Box>
            )}
          </Box>

          {/* Icons legend */}
          <Box p={4} borderBottom="1px solid" borderColor={c.panelBorder}>
            <Text fontSize="sm" color={c.textSecond} mb={3}>Icons:</Text>
            <VStack align="stretch" gap={3}>
              {iconItems.map(({key,checked,onChange,hex,Icon,label}) => (
                <Flex key={key} align="center" gap={3} cursor="pointer" as="label">
                  <Checkbox.Root checked={checked} onCheckedChange={e=>onChange(!!e.checked)} size="sm">
                    <Checkbox.HiddenInput/>
                    <Checkbox.Control style={{
                      borderColor: checked ? hex : c.inputBorder,
                      background:  checked ? hex : 'transparent',
                      transition: 'all 0.15s',
                    }}/>
                  </Checkbox.Root>
                  <HStack gap={2}>
                    <Flex w="20px" h="20px" borderRadius="full" align="center" justify="center" flexShrink={0} style={{ backgroundColor:hex }}>
                      <Icon size={11} color="white"/>
                    </Flex>
                    <Text fontSize="sm" color={c.textPrimary}>{label}</Text>
                  </HStack>
                </Flex>
              ))}
            </VStack>
          </Box>

          {/* Visualize */}
          <Box p={4} borderBottom="1px solid" borderColor={c.panelBorder}>
            <Text fontSize="sm" color={c.textSecond} mb={3}>Visualize:</Text>
            <VStack align="stretch" gap={3}>
              <Flex align="center" gap={3} cursor="pointer" as="label">
                <Checkbox.Root checked={showGrid} onCheckedChange={e=>setShowGrid(!!e.checked)} size="sm">
                  <Checkbox.HiddenInput/>
                  <Checkbox.Control style={{ borderColor:c.inputBorder }}/>
                </Checkbox.Root>
                <HStack gap={2}>
                  <Grid3X3 size={14} color={c.textSecond}/>
                  <Text fontSize="sm" color={c.textPrimary}>Grid</Text>
                </HStack>
              </Flex>

              <Flex align="center" gap={3} cursor="pointer" as="label">
                <Checkbox.Root checked={showColorByDanger} onCheckedChange={e=>setShowColorByDanger(!!e.checked)} size="sm">
                  <Checkbox.HiddenInput/>
                  <Checkbox.Control style={{ borderColor:c.inputBorder }}/>
                </Checkbox.Root>
                <HStack gap={2}>
                  <Layers size={14} color={c.textSecond}/>
                  <Text fontSize="sm" color={c.textPrimary}>Color area by danger</Text>
                </HStack>
              </Flex>

              {showColorByDanger && (
                <VStack align="stretch" gap={2} ml={7}>
                  {dangerRanges.map(({range,label,hex}) => (
                    <HStack key={range} gap={2}>
                      <Box w="11px" h="11px" borderRadius="full" flexShrink={0} style={{ backgroundColor:hex }}/>
                      <Text fontSize="xs" color={c.textSecond}>{range}</Text>
                      <Text fontSize="xs" color={c.textMuted} ml="auto">{label}</Text>
                    </HStack>
                  ))}
                </VStack>
              )}
            </VStack>
          </Box>

          {/* Zone count */}
          <Box p={4} mt="auto">
            <Box bg={c.sectionBg} borderRadius="lg" p={3} border="1px solid" borderColor={c.cardBorder} transition="background 0.2s">
              <Text fontSize="xs" color={c.textMuted} mb={2}>Zonas visíveis</Text>
              <Flex gap={3} flexWrap="wrap">
                {zoneCounts.map(({label,hex,filter}) => (
                  <HStack key={label} gap={1}>
                    <Box w="10px" h="10px" borderRadius="full" style={{ backgroundColor:hex }}/>
                    <Text fontSize="xs" color={c.textSecond}>{filteredZones.filter(filter).length} {label}</Text>
                  </HStack>
                ))}
              </Flex>
            </Box>
          </Box>
        </Flex>

        {/* Map */}
        <Box flex={1} position="relative">
          <PortugalMap
            zones={filteredZones}
            showGrid={showGrid}
            showColorByDanger={showColorByDanger}
            showForests={showForests}
            mapType={mapType}
            isDark={isDark}
          />
        </Box>
      </Flex>
    </>
  );
}