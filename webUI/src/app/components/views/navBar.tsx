import React, { useEffect, useState } from 'react';
import { Box, Flex, Text } from "@chakra-ui/react";
import { Sun, Moon } from "lucide-react";
import { getColors } from '../../utils/utils';
import { useToken } from '../../context/TokenContext';
import { UserModal } from '../components/UserModal';
import npLogoUrl from '../../../../images/NPIconNoBg.png';

export function NavBar(
    { isDark, setIsDark }: { isDark: boolean; setIsDark: React.Dispatch<React.SetStateAction<boolean>> }
) {
    const c = getColors(isDark);
    const { user, token, refreshToken } = useToken();
    const signedIn = Boolean(token);
    const [isUserModalOpen, setIsUserModalOpen] = useState(false);

    useEffect(() => {
        if (signedIn && !user) {
            refreshToken();

            console.log("user", user);
        }
    }, [signedIn]);

    return (
        <Flex
            as="nav" align="center" gap={3} px={6} py="10px"
            bg={c.navBg} borderBottom="1px solid" borderColor={c.navBorder}
            shadow="sm" flexShrink={0} transition="background 0.2s"
        >
            <img
                src={npLogoUrl}
                width="36"
                height="36"
                alt="Nature Protector"
                style={{ display: "block", objectFit: "contain" }}
            />
            <Text fontWeight="semibold" fontSize="lg" color={c.textPrimary} letterSpacing="wide">
                Nature Protector
            </Text>
            <Box flex={1} />
            <Box
                as="button"
                onClick={() => setIsUserModalOpen(true)}
                display="flex"
                alignItems="center"
                gap="8px"
                px={3}
                py="6px"
                borderRadius="full"
                bg={c.sectionBg}
                border="1px solid"
                borderColor={c.navBorder}
                cursor="pointer"
                transition="all 0.2s"
                _hover={{ opacity: 0.9 }}
            >
                <Box
                    width="26px"
                    height="26px"
                    borderRadius="999px"
                    display="grid"
                    placeItems="center"
                    fontWeight={700}
                    fontSize="xs"
                    color={c.textPrimary}
                    bg={signedIn ? "#22c55e" : c.toggleBg}
                >
                    {signedIn ? (user?.fullName?.[0] || user?.username?.[0] || "U") : "?"}
                </Box>
                <Text fontSize="xs" color={c.textSecond}>
                    {signedIn ? `Signed in${user?.fullName ? ` as ${user.fullName}` : ""}` : "Not signed in"}
                </Text>
            </Box>
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
            <UserModal
                isDark={isDark}
                user={user}
                isOpen={isUserModalOpen}
                onClose={() => setIsUserModalOpen(false)}
            />
        </Flex>
    );
}