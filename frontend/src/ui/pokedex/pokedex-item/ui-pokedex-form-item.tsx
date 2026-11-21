import { Box, Group, Stack } from '@mantine/core';
import React from "react";
import type { Gender } from '../../../data/sdk/model';
import { UIGender } from '../../icon/ui-gender';
import { UIPokedexIcons } from '../icons/ui-pokedex-icons';
import classes from './ui-pokedex-item.module.css';

type UIPokedexFormItemProps = {
  genders: Gender[];
  isSeen?: boolean;
  isSeenAlpha?: boolean;
  isCaught?: boolean;
  isOwned?: boolean;
  isOwnedShiny?: boolean;
  isMega?: boolean;
  isOwnedGigantamax?: boolean;
  children: React.ReactNode;
};

export const UIPokedexFormItem: React.FC<UIPokedexFormItemProps> = ({
  genders,
  isSeen,
  isSeenAlpha,
  isCaught,
  isOwned,
  isOwnedShiny,
  isMega,
  isOwnedGigantamax,
  children
}) => {

  return <Box pos='relative'>
    <Box className={isSeen ? undefined : classes.silhouette}>
      {children}
    </Box>

    <Group pos='absolute' top={0} right={0} gap='xs' p='sm'>
      {isOwned && <UIPokedexIcons.Owned size='xs' />}

      {isCaught && <UIPokedexIcons.Caught size='xs' />}
    </Group>
    <Stack pos='absolute' bottom={0} right={0} align='flex-end' gap='sm' p='sm'>
      {isMega && <UIPokedexIcons.Mega size='xs' />}

      {isOwnedGigantamax && <UIPokedexIcons.Dyna size='xs' />}

      {isSeenAlpha && <UIPokedexIcons.Alpha size='xs' />}

      {isOwnedShiny && <UIPokedexIcons.Shiny size='xs' />}

      <Group gap='xs'>
        {genders.map(gender => <UIGender key={gender} gender={gender} size='small' />)}
      </Group>
    </Stack>
  </Box>;
};
