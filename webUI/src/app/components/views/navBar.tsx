import React from 'react';
import { Box, Flex, Text } from "@chakra-ui/react";
import { Sun, Moon } from "lucide-react";
import { getColors } from '../../utils/utils';


export function NavBar(
    { isDark, setIsDark }: { isDark: boolean; setIsDark: React.Dispatch<React.SetStateAction<boolean>>}
) {
    const c = getColors(isDark);

    return (
        <Flex
            as="nav" align="center" gap={3} px={6} py="10px"
            bg={c.navBg} borderBottom="1px solid" borderColor={c.navBorder}
            shadow="sm" flexShrink={0} transition="background 0.2s"
        >
            <img
                src="images/NPIconNoBg.png"
                width="36px"
                height="36px"
                alt="Nature Protector"
            />
            <Text fontWeight="semibold" fontSize="lg" color={c.textPrimary} letterSpacing="wide">
                Nature Protector
            </Text>
            <Box flex={1} />
            <Box
                as="button" onClick={() => setIsDark(d => !d)}
                display="flex" alignItems="center" gap="6px"
                px={3} py="6px" borderRadius="full"
                bg={c.toggleBg} border="1px solid" borderColor={c.navBorder}
                cursor="pointer" transition="all 0.2s" _hover={{ opacity: 0.8 }}
            >
                {isDark
                    ? <><Sun size={14} color="#f59e0b" /><Text fontSize="xs" color={c.textSecond}>Light</Text></>
                    : <><Moon size={14} color="#6366f1" /><Text fontSize="xs" color={c.textSecond}>Dark</Text></>
                }
            </Box>
        </Flex>
    );
}