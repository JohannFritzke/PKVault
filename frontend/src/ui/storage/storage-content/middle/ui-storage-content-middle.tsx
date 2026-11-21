import { Box, Stack } from '@mantine/core';
import type React from 'react';

export const UIStorageContentMiddle: React.FC<React.PropsWithChildren> = ({ children }) => {
    return <Box
        h='100%'
        py='md'
    >
        <Stack h='100%' justify='center'>
            {children}
        </Stack>
    </Box>;
};
