import { useEffect, useState } from 'react';
import { Box, Flex, Heading } from '@chakra-ui/react';
import { getColors } from '../../utils/utils';

function grafanaKioskUrl(base: string, areaId: string) {
  const url = base.replace('???', areaId);
  const separator = url.includes('?') ? '&' : '?';
  return `${url}${separator}kiosk&nav=false`;
}

export function GrafanaStrip({
  isDark,
  areaId,
  ...c
}: {
  isDark: boolean;
  areaId: string;
} & ReturnType<typeof getColors>) {
  const [dashboardLinks, setDashboardLinks] = useState<string[]>([]);
  useEffect(() => {
    console.log('areaId:', areaId);
    fetch('/area_dashboards_links.txt')
      .then((r) => r.text())
      .then((text) => {
        const links = text.split('\n').filter((line) => line.trim());
        setDashboardLinks(links);
      })
      .catch((err) => console.error('Failed to load dashboards:', err));
  }, [areaId]);

  return (
    <Box
      bg={c.panelBg}
      borderBottom="1px solid"
      borderColor={c.panelBorder}
      flexShrink={0}
      transition="background 0.2s"
    // REMOVED resize="vertical" to avoid manual height override
    >
      <Box px={6} pt={4} pb={1}>
        <Heading size="xl" color={c.textPrimary} fontFamily="serif">
          Grafana Dashboards
        </Heading>
      </Box>

      <Flex
        gap={4}
        px={6}
        pb={4}
        pt={3}
        overflowX="auto" // Changed from scroll to auto for cleaner look
        align="stretch" // Ensures all boxes have the same height if one is taller
        css={{
          scrollbarWidth: 'thin',
          scrollbarColor: `${isDark ? '#2d3547' : '#d1d5db'} transparent`,
        }}
      >
        {dashboardLinks.map((dash, index) => (
          <Box
            key={dash}
            minW="450px"
            flex="1"
            minH="500px"
            borderRadius="md"
            overflow="hidden"
            border="1px solid"
            borderColor={c.panelBorder}
          >
            <iframe
              src={grafanaKioskUrl(dash, areaId)}
              width="100%"
              height="100%" // Now fills the 500px minH of the parent Box
              style={{ border: 0, display: 'block' }}
              title={`Dashboard ${index}`}
              loading="lazy"
            ></iframe>
          </Box>
        ))}
      </Flex>
    </Box>
  );
}

export function AreaRisk({
  isDark,
  areaId,
  ...c
}: {
  isDark: boolean;
  areaId: string;
} & ReturnType<typeof getColors>) {
  const [riskLinks, setRiskLinks] = useState<string[]>([]);
  useEffect(() => {
    console.log('areaId:', areaId);
    fetch('/area_risk_link.txt')
      .then((r) => r.text())
      .then((text) => {
        const links = text.split('\n').filter((line) => line.trim());
        setRiskLinks(links);
      })
      .catch((err) => console.error('Failed to load dashboards:', err));
  }, [areaId]);

  return (
    <Flex
      bg={c.panelBg}
      borderBottom="1px solid"
      borderColor={c.panelBorder}
      gap={4}
      px={6}
      py={4}
      align="stretch"
      minH="350px"
      transition="background 0.2s"
    >
      {riskLinks.map((link, index) => (
        <Box
          key={link}
          {...(index === 0
            ? { w: '600px', h: '600px', flex: 'none' }
            : { flex: '1' }
          )}
          borderRadius="md"
          overflow="hidden"
          border="1px solid"
          borderColor={c.panelBorder}
        >
          <iframe
            src={grafanaKioskUrl(link, areaId)}
            width="100%"
            height="100%"
            style={{ border: 0, display: 'block' }}
            title={`Area Risk Dashboard ${index + 1}`}
            loading="lazy"
          ></iframe>
        </Box>
      ))}
    </Flex>
  );
}
